using System.Data;
using HostelManagement.Models;

namespace HostelManagement.Data;

public static class RoomRepository
{
    // Capacity comes from the sharing type; occupancy is counted from current allocations.
    private const string SelectRooms =
        "SELECT r.[RoomId], r.[HostelId], r.[RoomNumber], r.[Floor], r.[SharingTypeId], r.[Gender], r.[IsActive], r.[Remarks], " +
        "s.[SharingName], s.[Capacity], " +
        "(SELECT COUNT(*) FROM [RoomAllocation] AS a WHERE a.[RoomId] = r.[RoomId] AND a.[Status] = ?) AS [Occupied] " +
        "FROM [Room] AS r INNER JOIN [SharingType] AS s ON r.[SharingTypeId] = s.[SharingTypeId]";

    public static List<Room> GetForHostel(int hostelId) =>
        Db.Query(SelectRooms + " WHERE r.[HostelId] = ?", Map,
            Db.Param("@Status", AllocationStatus.Current),
            Db.Param("@HostelId", hostelId));

    public static Room? Get(int roomId) =>
        Db.Query(SelectRooms + " WHERE r.[RoomId] = ?", Map,
            Db.Param("@Status", AllocationStatus.Current),
            Db.Param("@RoomId", roomId)).FirstOrDefault();

    public static int Insert(Room room) =>
        Db.Insert(
            "INSERT INTO [Room] ([HostelId], [RoomNumber], [Floor], [SharingTypeId], [Gender], [IsActive], [Remarks]) " +
            "VALUES (?, ?, ?, ?, ?, ?, ?)",
            Db.Param("@HostelId", room.HostelId),
            Db.Param("@RoomNumber", room.RoomNumber),
            Db.OptionalText("@Floor", room.Floor),
            Db.Param("@SharingTypeId", room.SharingTypeId),
            Db.Param("@Gender", room.Gender),
            Db.Param("@IsActive", room.IsActive),
            Db.OptionalText("@Remarks", room.Remarks));

    public static int Update(Room room) =>
        Db.Execute(
            "UPDATE [Room] SET [RoomNumber] = ?, [Floor] = ?, [SharingTypeId] = ?, [Gender] = ?, [IsActive] = ?, [Remarks] = ? " +
            "WHERE [RoomId] = ?",
            Db.Param("@RoomNumber", room.RoomNumber),
            Db.OptionalText("@Floor", room.Floor),
            Db.Param("@SharingTypeId", room.SharingTypeId),
            Db.Param("@Gender", room.Gender),
            Db.Param("@IsActive", room.IsActive),
            Db.OptionalText("@Remarks", room.Remarks),
            Db.Param("@RoomId", room.RoomId));

    public static int Delete(int roomId) =>
        Db.Execute("DELETE FROM [Room] WHERE [RoomId] = ?", Db.Param("@RoomId", roomId));

    /// <summary>True when another room of the same hostel already uses this number (ignoring case).</summary>
    public static bool NumberExists(int hostelId, string roomNumber, int exceptRoomId) =>
        Convert.ToInt32(Db.Scalar(
            "SELECT COUNT(*) FROM [Room] WHERE [HostelId] = ? AND UCASE([RoomNumber]) = UCASE(?) AND [RoomId] <> ?",
            Db.Param("@HostelId", hostelId),
            Db.Param("@RoomNumber", roomNumber),
            Db.Param("@RoomId", exceptRoomId))) > 0;

    /// <summary>All allocations ever made to the room (current and past).</summary>
    public static int CountAllocations(int roomId) =>
        Convert.ToInt32(Db.Scalar(
            "SELECT COUNT(*) FROM [RoomAllocation] WHERE [RoomId] = ?",
            Db.Param("@RoomId", roomId)));

    private static Room Map(IDataRecord record) => new()
    {
        RoomId = record.GetInt("RoomId"),
        HostelId = record.GetInt("HostelId"),
        RoomNumber = record.GetText("RoomNumber"),
        Floor = record.GetText("Floor"),
        SharingTypeId = record.GetInt("SharingTypeId"),
        Gender = record.GetText("Gender"),
        IsActive = record.GetBool("IsActive"),
        Remarks = record.GetText("Remarks"),
        SharingName = record.GetText("SharingName"),
        Capacity = record.GetInt("Capacity"),
        Occupied = record.GetInt("Occupied"),
    };
}
