using System.Data;
using HostelManagement.Models;

namespace HostelManagement.Data;

/// <summary>Reads and saves the single Hostel row.</summary>
public static class HostelRepository
{
    public static HostelDetails? Get() =>
        Db.Query("SELECT TOP 1 * FROM [Hostel] ORDER BY [HostelId]", Map).FirstOrDefault();

    public static int Insert(HostelDetails hostel) =>
        Db.Insert(
            "INSERT INTO [Hostel] ([HostelName], [Address], [Phone], [Email], [CreatedDate]) VALUES (?, ?, ?, ?, ?)",
            Db.Param("@HostelName", hostel.HostelName),
            Db.OptionalText("@Address", hostel.Address),
            Db.OptionalText("@Phone", hostel.Phone),
            Db.OptionalText("@Email", hostel.Email),
            Db.Param("@CreatedDate", hostel.CreatedDate));

    public static void Update(HostelDetails hostel) =>
        Db.Execute(
            "UPDATE [Hostel] SET [HostelName] = ?, [Address] = ?, [Phone] = ?, [Email] = ?, [UpdatedDate] = ? " +
            "WHERE [HostelId] = ?",
            Db.Param("@HostelName", hostel.HostelName),
            Db.OptionalText("@Address", hostel.Address),
            Db.OptionalText("@Phone", hostel.Phone),
            Db.OptionalText("@Email", hostel.Email),
            Db.Param("@UpdatedDate", hostel.UpdatedDate),
            Db.Param("@HostelId", hostel.HostelId));

    private static HostelDetails Map(IDataRecord record) => new()
    {
        HostelId = record.GetInt("HostelId"),
        HostelName = record.GetText("HostelName"),
        Address = record.GetText("Address"),
        Phone = record.GetText("Phone"),
        Email = record.GetText("Email"),
        CreatedDate = record.GetDate("CreatedDate"),
        UpdatedDate = record.GetNullableDate("UpdatedDate"),
    };
}
