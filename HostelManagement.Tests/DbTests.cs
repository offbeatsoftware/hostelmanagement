using System.Data.OleDb;
using HostelManagement.Data;
using Xunit;

namespace HostelManagement.Tests;

public sealed class DbTests : TestDatabase
{
    [Fact]
    public void Insert_ReturnsNewAutoNumberIds()
    {
        int first = AddStudent("First");
        int second = AddStudent("Second");

        Assert.True(first > 0);
        Assert.Equal(first + 1, second);
    }

    [Fact]
    public void Param_RoundTripsTextNumberMoneyBoolAndNull()
    {
        int doubleSharing = SharingTypeId(capacity: 2);
        Db.Execute("UPDATE [SharingType] SET [Rent] = ? WHERE [SharingTypeId] = ?",
            Db.Param("@Rent", 4500.75m), Db.Param("@SharingTypeId", doubleSharing));

        int id = Db.Insert(
            "INSERT INTO [Room] ([RoomNumber], [Floor], [SharingTypeId], [IsActive], [Remarks]) VALUES (?, ?, ?, ?, ?)",
            Db.Param("@RoomNumber", "101"),
            Db.Param("@Floor", "Ground"),
            Db.Param("@SharingTypeId", doubleSharing),
            Db.Param("@IsActive", true),
            Db.Param("@Remarks", null));

        var room = Db.Query(
            "SELECT r.*, s.[Rent] FROM [Room] AS r INNER JOIN [SharingType] AS s " +
            "ON r.[SharingTypeId] = s.[SharingTypeId] WHERE r.[RoomId] = ?",
            r => new
            {
                Number = r.GetText("RoomNumber"),
                Floor = r.GetText("Floor"),
                SharingTypeId = r.GetInt("SharingTypeId"),
                Rent = r.GetMoney("Rent"),
                Active = r.GetBool("IsActive"),
                RemarksIsNull = r["Remarks"] is DBNull,
            },
            Db.Param("@RoomId", id)).Single();

        Assert.Equal("101", room.Number);
        Assert.Equal("Ground", room.Floor);
        Assert.Equal(doubleSharing, room.SharingTypeId);
        Assert.Equal(4500.75m, room.Rent);
        Assert.True(room.Active);
        Assert.True(room.RemarksIsNull);
    }

    [Fact]
    public void Param_SavesDatesIncludingMilliseconds()
    {
        // DateTime.Now has milliseconds, which cause "Data type mismatch" unless OleDbType.Date is used.
        DateTime now = DateTime.Now;
        int id = Db.Insert(
            "INSERT INTO [Student] ([StudentName], [CollegeId], [DateOfBirth], [AdmissionDate], [Status]) " +
            "VALUES (?, ?, ?, ?, ?)",
            Db.Param("@StudentName", "Date Test"),
            Db.Param("@CollegeId", CollegeId),
            Db.Param("@DateOfBirth", new DateTime(2006, 2, 28)),
            Db.Param("@AdmissionDate", now),
            Db.Param("@Status", "Active"));

        var dates = Db.Query(
            "SELECT [DateOfBirth], [AdmissionDate] FROM [Student] WHERE [StudentId] = ?",
            r => (Birth: r.GetNullableDate("DateOfBirth"), Admission: r.GetDate("AdmissionDate")),
            Db.Param("@StudentId", id)).Single();

        Assert.Equal(new DateTime(2006, 2, 28), dates.Birth);
        Assert.True(Math.Abs((dates.Admission - now).TotalSeconds) < 1);
    }

    [Fact]
    public void Param_TreatsInjectionAttemptAsPlainText()
    {
        const string hostile = "x'); DELETE FROM [Student]; --";
        AddStudent("Keep Me");
        AddStudent(hostile);

        Assert.Equal(2, Count("Student"));
        Assert.Equal(1, Convert.ToInt32(Db.Scalar(
            "SELECT COUNT(*) FROM [Student] WHERE [StudentName] = ?", Db.Param("@StudentName", hostile))));
    }

    [Fact]
    public void Execute_ReturnsAffectedRowCount()
    {
        int id = AddStudent("Before");

        int updated = Db.Execute("UPDATE [Student] SET [StudentName] = ? WHERE [StudentId] = ?",
            Db.Param("@StudentName", "After"), Db.Param("@StudentId", id));
        int deleted = Db.Execute("DELETE FROM [Student] WHERE [StudentId] = ?", Db.Param("@StudentId", id));

        Assert.Equal(1, updated);
        Assert.Equal(1, deleted);
        Assert.Equal(0, Count("Student"));
    }

    [Fact]
    public void UniqueRoomNumber_IsEnforcedAndDetected()
    {
        const string sql = "INSERT INTO [Room] ([RoomNumber], [SharingTypeId], [IsActive]) VALUES (?, ?, ?)";
        int single = SharingTypeId(capacity: 1);
        OleDbParameter[] Room() =>
        [
            Db.Param("@RoomNumber", "201"), Db.Param("@SharingTypeId", single), Db.Param("@IsActive", true),
        ];

        Db.Execute(sql, Room());
        var ex = Assert.Throws<OleDbException>(() => Db.Execute(sql, Room()));

        string details = $"Message={ex.Message} ErrorCode={ex.ErrorCode} Errors=" +
            string.Join(" | ", ex.Errors.Cast<OleDbError>()
                .Select(error => $"SQLState={error.SQLState} NativeError={error.NativeError} Message={error.Message}"));
        Assert.True(Db.IsDuplicateKeyError(ex), details);
    }

    [Fact]
    public void ForeignKey_RejectsParentForUnknownStudent()
    {
        var ex = Assert.Throws<OleDbException>(() => Db.Execute(
            "INSERT INTO [Parent] ([StudentId], [ParentName], [IsPrimaryContact]) VALUES (?, ?, ?)",
            Db.Param("@StudentId", 9999), Db.Param("@ParentName", "Nobody"), Db.Param("@IsPrimaryContact", true)));

        Assert.False(Db.IsDuplicateKeyError(ex));
        Assert.Equal(0, Count("Parent"));
    }

    [Fact]
    public void RequiredColumn_RejectsNull()
    {
        Assert.Throws<OleDbException>(() => Db.Execute(
            "INSERT INTO [Student] ([StudentName], [CollegeId], [Status]) VALUES (?, ?, ?)",
            Db.Param("@StudentName", null), Db.Param("@CollegeId", CollegeId), Db.Param("@Status", "Active")));
    }

    [Fact]
    public void Student_WithoutCollege_IsRejected()
    {
        Assert.Throws<OleDbException>(() => Db.Execute(
            "INSERT INTO [Student] ([StudentName], [Status]) VALUES (?, ?)",
            Db.Param("@StudentName", "No College"), Db.Param("@Status", "Active")));
    }

    [Fact]
    public void InTransaction_CommitsAllStatements()
    {
        int collegeId = CollegeId;
        int studentId = Db.InTransaction((connection, transaction) =>
        {
            int id = Db.Insert(connection, transaction,
                "INSERT INTO [Student] ([StudentName], [CollegeId], [Status]) VALUES (?, ?, ?)",
                Db.Param("@StudentName", "With Parent"), Db.Param("@CollegeId", collegeId), Db.Param("@Status", "Active"));
            Db.Execute(connection, transaction,
                "INSERT INTO [Parent] ([StudentId], [ParentName], [IsPrimaryContact]) VALUES (?, ?, ?)",
                Db.Param("@StudentId", id), Db.Param("@ParentName", "Parent"), Db.Param("@IsPrimaryContact", true));
            return id;
        });

        Assert.True(studentId > 0);
        Assert.Equal(1, Count("Student"));
        Assert.Equal(1, Count("Parent"));
    }

    [Fact]
    public void InTransaction_RollsBackEverythingOnError()
    {
        int collegeId = CollegeId;
        Assert.Throws<InvalidOperationException>(() => Db.InTransaction((connection, transaction) =>
        {
            Db.Execute(connection, transaction,
                "INSERT INTO [Student] ([StudentName], [CollegeId], [Status]) VALUES (?, ?, ?)",
                Db.Param("@StudentName", "Rolled Back"), Db.Param("@CollegeId", collegeId), Db.Param("@Status", "Active"));
            throw new InvalidOperationException("Simulated failure");
        }));

        Assert.Equal(0, Count("Student"));
    }

    [Fact]
    public void OpenConnection_MissingDatabaseFile_ThrowsFriendlyDatabaseException()
    {
        Db.Configure(Db.ProviderName, Path.Combine(DataFolder, "does-not-exist.accdb"));

        var ex = Assert.Throws<DatabaseException>(() => Db.OpenConnection());

        Assert.DoesNotContain("OleDb", ex.Message);
        Assert.NotNull(ex.InnerException);
    }
}
