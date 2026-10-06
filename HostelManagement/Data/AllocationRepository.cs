using System.Data;
using System.Data.OleDb;
using HostelManagement.Models;

namespace HostelManagement.Data;

public static class AllocationRepository
{
    private const string SelectAllocations =
        "SELECT a.*, s.[StudentName], s.[Mobile], r.[RoomNumber] FROM ([RoomAllocation] AS a " +
        "INNER JOIN [Student] AS s ON a.[StudentId] = s.[StudentId]) " +
        "INNER JOIN [Room] AS r ON a.[RoomId] = r.[RoomId]";

    /// <summary>Allocations to the hostel's rooms; only the current ones unless <paramref name="includeHistory"/>.</summary>
    public static List<RoomAllocation> GetForHostel(int hostelId, bool includeHistory) =>
        includeHistory
            ? Db.Query(SelectAllocations + " WHERE r.[HostelId] = ?", Map, Db.Param("@HostelId", hostelId))
            : Db.Query(SelectAllocations + " WHERE r.[HostelId] = ? AND a.[Status] = ?", Map,
                Db.Param("@HostelId", hostelId),
                Db.Param("@Status", AllocationStatus.Current));

    /// <summary>The student's current allocation, or null when the student has no room.</summary>
    public static RoomAllocation? GetCurrentForStudent(int studentId) =>
        Db.Query(SelectAllocations + " WHERE a.[StudentId] = ? AND a.[Status] = ?", Map,
            Db.Param("@StudentId", studentId),
            Db.Param("@Status", AllocationStatus.Current)).FirstOrDefault();

    /// <summary>All of the student's allocations, oldest first.</summary>
    public static List<RoomAllocation> GetForStudent(int studentId) =>
        Db.Query(SelectAllocations + " WHERE a.[StudentId] = ? ORDER BY a.[CheckInDate], a.[AllocationId]", Map,
            Db.Param("@StudentId", studentId));

    public static int Insert(OleDbConnection connection, OleDbTransaction transaction, RoomAllocation allocation) =>
        Db.Insert(connection, transaction,
            "INSERT INTO [RoomAllocation] ([StudentId], [RoomId], [CheckInDate], [Status], [Remarks], [BedNumber]) " +
            "VALUES (?, ?, ?, ?, ?, ?)",
            Db.Param("@StudentId", allocation.StudentId),
            Db.Param("@RoomId", allocation.RoomId),
            Db.Param("@CheckInDate", allocation.CheckInDate),
            Db.Param("@Status", AllocationStatus.Current),
            Db.OptionalText("@Remarks", allocation.Remarks),
            Db.Param("@BedNumber", allocation.BedNumber));

    /// <summary>The bed numbers of the students currently in the room (read inside the check-in transaction).</summary>
    public static List<int> GetTakenBeds(OleDbConnection? connection, OleDbTransaction? transaction, int roomId)
    {
        const string sql = "SELECT [BedNumber] FROM [RoomAllocation] WHERE [RoomId] = ? AND [Status] = ? AND [BedNumber] IS NOT NULL";
        Func<IDataRecord, int> map = r => r.GetInt("BedNumber");
        return connection is null
            ? Db.Query(sql, map, Db.Param("@RoomId", roomId), Db.Param("@Status", AllocationStatus.Current))
            : Db.Query(connection, transaction, sql, map, Db.Param("@RoomId", roomId), Db.Param("@Status", AllocationStatus.Current));
    }

    /// <summary>Ends a current allocation (transfer or check-out).</summary>
    public static int Close(OleDbConnection connection, OleDbTransaction transaction, int allocationId,
        DateTime checkOutDate, string status, string remarks) =>
        Db.Execute(connection, transaction,
            "UPDATE [RoomAllocation] SET [CheckOutDate] = ?, [Status] = ?, [Remarks] = ? " +
            "WHERE [AllocationId] = ? AND [Status] = ?",
            Db.Param("@CheckOutDate", checkOutDate),
            Db.Param("@Status", status),
            Db.OptionalText("@Remarks", remarks),
            Db.Param("@AllocationId", allocationId),
            Db.Param("@CurrentStatus", AllocationStatus.Current));

    public static void SetStudentStatus(OleDbConnection connection, OleDbTransaction transaction, int studentId, string status) =>
        Db.Execute(connection, transaction, "UPDATE [Student] SET [Status] = ? WHERE [StudentId] = ?",
            Db.Param("@Status", status),
            Db.Param("@StudentId", studentId));

    private static RoomAllocation Map(IDataRecord record) => new()
    {
        AllocationId = record.GetInt("AllocationId"),
        StudentId = record.GetInt("StudentId"),
        RoomId = record.GetInt("RoomId"),
        CheckInDate = record.GetDate("CheckInDate"),
        CheckOutDate = record.GetNullableDate("CheckOutDate"),
        Status = record.GetText("Status"),
        Remarks = record.GetText("Remarks"),
        BedNumber = record["BedNumber"] is DBNull ? null : record.GetInt("BedNumber"),
        StudentName = record.GetText("StudentName"),
        StudentMobile = record.GetText("Mobile"),
        RoomNumber = record.GetText("RoomNumber"),
    };
}
