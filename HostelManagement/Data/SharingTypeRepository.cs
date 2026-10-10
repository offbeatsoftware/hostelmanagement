using System.Data;
using System.Data.OleDb;
using HostelManagement.Models;

namespace HostelManagement.Data;

public static class SharingTypeRepository
{
    public static List<SharingType> GetForHostel(int hostelId) =>
        Db.Query("SELECT * FROM [SharingType] WHERE [HostelId] = ? ORDER BY [Capacity]", Map,
            Db.Param("@HostelId", hostelId));

    public static SharingType? Get(int sharingTypeId) =>
        Db.Query("SELECT * FROM [SharingType] WHERE [SharingTypeId] = ?", Map,
            Db.Param("@SharingTypeId", sharingTypeId)).FirstOrDefault();

    /// <summary>Adds Single, Double and Triple sharing for a new hostel.</summary>
    public static void InsertDefaults(OleDbConnection connection, OleDbTransaction transaction, int hostelId)
    {
        foreach ((string name, int capacity) in DatabaseSchema.DefaultSharingTypes)
        {
            Db.Execute(connection, transaction,
                "INSERT INTO [SharingType] ([HostelId], [SharingName], [Capacity]) VALUES (?, ?, ?)",
                Db.Param("@HostelId", hostelId),
                Db.Param("@SharingName", name),
                Db.Param("@Capacity", capacity));
        }
    }

    public static void DeleteForHostel(OleDbConnection connection, OleDbTransaction transaction, int hostelId) =>
        Db.Execute(connection, transaction, "DELETE FROM [SharingType] WHERE [HostelId] = ?",
            Db.Param("@HostelId", hostelId));

    private static SharingType Map(IDataRecord record) => new()
    {
        SharingTypeId = record.GetInt("SharingTypeId"),
        HostelId = record.GetInt("HostelId"),
        SharingName = record.GetText("SharingName"),
        Capacity = record.GetInt("Capacity"),
    };
}
