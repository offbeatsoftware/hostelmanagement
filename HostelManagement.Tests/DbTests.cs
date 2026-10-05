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
        int id = Db.Insert(
            "INSERT INTO [Room] ([RoomNumber], [Floor], [Capacity], [SharingType], [Rent], [IsActive], [Remarks]) " +
            "VALUES (?, ?, ?, ?, ?, ?, ?)",
            Db.Param("@RoomNumber", "101"),
            Db.Param("@Floor", "Ground"),
            Db.Param("@Capacity", 2),
            Db.Param("@SharingType", "Double"),
            Db.Param("@Rent", 4500.75m),
            Db.Param("@IsActive", true),
            Db.Param("@Remarks", null));

        var room = Db.Query(
            "SELECT * FROM [Room] WHERE [RoomId] = ?",
            r => new
            {
                Number = r.GetText("RoomNumber"),
                Floor = r.GetText("Floor"),
                Capacity = r.GetInt("Capacity"),
                Rent = r.GetMoney("Rent"),
                Active = r.GetBool("IsActive"),
                RemarksIsNull = r["Remarks"] is DBNull,
            },
            Db.Param("@RoomId", id)).Single();

        Assert.Equal("101", room.Number);
        Assert.Equal("Ground", room.Floor);
        Assert.Equal(2, room.Capacity);
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
            "INSERT INTO [Student] ([StudentName], [DateOfBirth], [AdmissionDate], [Status]) VALUES (?, ?, ?, ?)",
            Db.Param("@StudentName", "Date Test"),
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
        const string sql = "INSERT INTO [Room] ([RoomNumber], [Capacity], [SharingType], [Rent], [IsActive]) " +
                           "VALUES (?, ?, ?, ?, ?)";
        OleDbParameter[] Room() =>
        [
            Db.Param("@RoomNumber", "201"), Db.Param("@Capacity", 1), Db.Param("@SharingType", "Single"),
            Db.Param("@Rent", 5000m), Db.Param("@IsActive", true),
        ];

        Db.Execute(sql, Room());
        var ex = Assert.Throws<OleDbException>(() => Db.Execute(sql, Room()));

        Assert.True(Db.IsDuplicateKeyError(ex));
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
            "INSERT INTO [Student] ([StudentName], [Status]) VALUES (?, ?)",
            Db.Param("@StudentName", null), Db.Param("@Status", "Active")));
    }

    [Fact]
    public void InTransaction_CommitsAllStatements()
    {
        int studentId = Db.InTransaction((connection, transaction) =>
        {
            int id = Db.Insert(connection, transaction,
                "INSERT INTO [Student] ([StudentName], [Status]) VALUES (?, ?)",
                Db.Param("@StudentName", "With Parent"), Db.Param("@Status", "Active"));
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
        Assert.Throws<InvalidOperationException>(() => Db.InTransaction((connection, transaction) =>
        {
            Db.Execute(connection, transaction,
                "INSERT INTO [Student] ([StudentName], [Status]) VALUES (?, ?)",
                Db.Param("@StudentName", "Rolled Back"), Db.Param("@Status", "Active"));
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
