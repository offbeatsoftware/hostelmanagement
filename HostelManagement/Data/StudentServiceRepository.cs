using System.Data;
using System.Data.OleDb;
using HostelManagement.Models;

namespace HostelManagement.Data;

/// <summary>Which extra services (such as transport) a student uses, with start and end dates for billing.</summary>
public static class StudentServiceRepository
{
    private const string SelectUses =
        "SELECT u.*, v.[ServiceName], v.[MonthlyRate] FROM [StudentService] AS u " +
        "INNER JOIN [Service] AS v ON u.[ServiceId] = v.[ServiceId]";

    /// <summary>The services the student uses now (no end date).</summary>
    public static List<StudentServiceUse> GetCurrentForStudent(int studentId) =>
        Db.Query(SelectUses + " WHERE u.[StudentId] = ? AND u.[EndDate] IS NULL ORDER BY v.[ServiceName]", Map,
            Db.Param("@StudentId", studentId));

    /// <summary>All of the student's service uses, past and present.</summary>
    public static List<StudentServiceUse> GetForStudent(int studentId) =>
        Db.Query(SelectUses + " WHERE u.[StudentId] = ? ORDER BY u.[StartDate]", Map, Db.Param("@StudentId", studentId));

    public static void Start(OleDbConnection connection, OleDbTransaction transaction, int studentId, int serviceId, DateTime startDate) =>
        Db.Execute(connection, transaction,
            "INSERT INTO [StudentService] ([StudentId], [ServiceId], [StartDate]) VALUES (?, ?, ?)",
            Db.Param("@StudentId", studentId),
            Db.Param("@ServiceId", serviceId),
            Db.Param("@StartDate", startDate));

    public static void Stop(OleDbConnection connection, OleDbTransaction transaction, int studentServiceId, DateTime endDate) =>
        Db.Execute(connection, transaction,
            "UPDATE [StudentService] SET [EndDate] = ? WHERE [StudentServiceId] = ?",
            Db.Param("@EndDate", endDate),
            Db.Param("@StudentServiceId", studentServiceId));

    public static void Delete(OleDbConnection connection, OleDbTransaction transaction, int studentServiceId) =>
        Db.Execute(connection, transaction, "DELETE FROM [StudentService] WHERE [StudentServiceId] = ?",
            Db.Param("@StudentServiceId", studentServiceId));

    public static void DeleteForStudent(OleDbConnection connection, OleDbTransaction transaction, int studentId) =>
        Db.Execute(connection, transaction, "DELETE FROM [StudentService] WHERE [StudentId] = ?",
            Db.Param("@StudentId", studentId));

    private static StudentServiceUse Map(IDataRecord record) => new()
    {
        StudentServiceId = record.GetInt("StudentServiceId"),
        StudentId = record.GetInt("StudentId"),
        ServiceId = record.GetInt("ServiceId"),
        StartDate = record.GetDate("StartDate"),
        EndDate = record.GetNullableDate("EndDate"),
        ServiceName = record.GetText("ServiceName"),
        MonthlyRate = record.GetMoney("MonthlyRate"),
    };
}
