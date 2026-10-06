using System.Data;
using System.Data.OleDb;

namespace HostelManagement.Data;

public static class AttendanceRepository
{
    /// <summary>A saved attendance record (the student's name and other details come from the sheet).</summary>
    public sealed record Saved(int AttendanceId, int StudentId, bool IsPresent, string Remarks, DateTime? ParentEmailedDate);

    /// <summary>The saved records of the hostel's students for a date.</summary>
    public static List<Saved> GetForDate(int hostelId, DateTime date) =>
        Db.Query(
            "SELECT a.* FROM ([Attendance] AS a INNER JOIN [Student] AS s ON a.[StudentId] = s.[StudentId]) " +
            "INNER JOIN [College] AS c ON s.[CollegeId] = c.[CollegeId] WHERE c.[HostelId] = ? AND a.[AttendanceDate] = ?",
            Map,
            Db.Param("@HostelId", hostelId),
            Db.Param("@AttendanceDate", date.Date));

    public static Saved? Get(int attendanceId) =>
        Db.Query("SELECT * FROM [Attendance] WHERE [AttendanceId] = ?", Map, Db.Param("@AttendanceId", attendanceId))
            .FirstOrDefault();

    /// <summary>Adds or updates the student's record for the date (the email date is kept).</summary>
    public static void Upsert(OleDbConnection connection, OleDbTransaction transaction, int studentId, DateTime date,
        bool isPresent, string remarks, DateTime markedDate)
    {
        int updated = Db.Execute(connection, transaction,
            "UPDATE [Attendance] SET [IsPresent] = ?, [Remarks] = ?, [MarkedDate] = ? WHERE [StudentId] = ? AND [AttendanceDate] = ?",
            Db.Param("@IsPresent", isPresent),
            Db.OptionalText("@Remarks", remarks),
            Db.Param("@MarkedDate", markedDate),
            Db.Param("@StudentId", studentId),
            Db.Param("@AttendanceDate", date.Date));
        if (updated == 0)
        {
            Db.Execute(connection, transaction,
                "INSERT INTO [Attendance] ([StudentId], [AttendanceDate], [IsPresent], [Remarks], [MarkedDate]) VALUES (?, ?, ?, ?, ?)",
                Db.Param("@StudentId", studentId),
                Db.Param("@AttendanceDate", date.Date),
                Db.Param("@IsPresent", isPresent),
                Db.OptionalText("@Remarks", remarks),
                Db.Param("@MarkedDate", markedDate));
        }
    }

    public static void SetParentEmailed(int attendanceId, DateTime sentDate) =>
        Db.Execute("UPDATE [Attendance] SET [ParentEmailedDate] = ? WHERE [AttendanceId] = ?",
            Db.Param("@ParentEmailedDate", sentDate),
            Db.Param("@AttendanceId", attendanceId));

    private static Saved Map(IDataRecord record) => new(
        record.GetInt("AttendanceId"),
        record.GetInt("StudentId"),
        record.GetBool("IsPresent"),
        record.GetText("Remarks"),
        record.GetNullableDate("ParentEmailedDate"));
}
