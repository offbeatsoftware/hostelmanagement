using System.Data;
using System.Data.OleDb;
using HostelManagement.Models;

namespace HostelManagement.Data;

public static class HostelRepository
{
    private const string SelectHostels =
        "SELECT h.*, " +
        "(SELECT COUNT(*) FROM [College] AS c WHERE c.[HostelId] = h.[HostelId]) AS [CollegeCount], " +
        "(SELECT COUNT(*) FROM [Room] AS r WHERE r.[HostelId] = h.[HostelId]) AS [RoomCount] " +
        "FROM [Hostel] AS h";

    /// <summary>All hostels ordered by name, with their college and room counts.</summary>
    public static List<Hostel> GetAll() =>
        Db.Query(SelectHostels + " ORDER BY h.[HostelName]", Map);

    public static Hostel? Get(int hostelId) =>
        Db.Query(SelectHostels + " WHERE h.[HostelId] = ?", Map, Db.Param("@HostelId", hostelId)).FirstOrDefault();

    public static int Insert(OleDbConnection connection, OleDbTransaction transaction, Hostel hostel) =>
        Db.Insert(connection, transaction,
            "INSERT INTO [Hostel] ([HostelName], [Address], [Phone], [Email], [CreatedDate]) " +
            "VALUES (?, ?, ?, ?, ?)",
            Db.Param("@HostelName", hostel.HostelName),
            Db.OptionalText("@Address", hostel.Address),
            Db.OptionalText("@Phone", hostel.Phone),
            Db.OptionalText("@Email", hostel.Email),
            Db.Param("@CreatedDate", hostel.CreatedDate));

    public static int Update(Hostel hostel) =>
        Db.Execute(
            "UPDATE [Hostel] SET [HostelName] = ?, [Address] = ?, [Phone] = ?, [Email] = ?, " +
            "[UpdatedDate] = ? " +
            "WHERE [HostelId] = ?",
            Db.Param("@HostelName", hostel.HostelName),
            Db.OptionalText("@Address", hostel.Address),
            Db.OptionalText("@Phone", hostel.Phone),
            Db.OptionalText("@Email", hostel.Email),
            Db.Param("@UpdatedDate", hostel.UpdatedDate),
            Db.Param("@HostelId", hostel.HostelId));

    public static int Delete(OleDbConnection connection, OleDbTransaction transaction, int hostelId) =>
        Db.Execute(connection, transaction, "DELETE FROM [Hostel] WHERE [HostelId] = ?",
            Db.Param("@HostelId", hostelId));

    /// <summary>True when another hostel already has this name (ignoring case).</summary>
    public static bool NameExists(string hostelName, int exceptHostelId) =>
        Convert.ToInt32(Db.Scalar(
            "SELECT COUNT(*) FROM [Hostel] WHERE UCASE([HostelName]) = UCASE(?) AND [HostelId] <> ?",
            Db.Param("@HostelName", hostelName),
            Db.Param("@HostelId", exceptHostelId))) > 0;

    private static Hostel Map(IDataRecord record) => new()
    {
        HostelId = record.GetInt("HostelId"),
        HostelName = record.GetText("HostelName"),
        Address = record.GetText("Address"),
        Phone = record.GetText("Phone"),
        Email = record.GetText("Email"),
        CreatedDate = record.GetDate("CreatedDate"),
        UpdatedDate = record.GetNullableDate("UpdatedDate"),
        CollegeCount = record.GetInt("CollegeCount"),
        RoomCount = record.GetInt("RoomCount"),
    };
}
