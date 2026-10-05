using System.Data;
using HostelManagement.Models;

namespace HostelManagement.Data;

public static class SharingTypeRepository
{
    public static List<SharingType> GetAll() =>
        Db.Query("SELECT * FROM [SharingType] ORDER BY [Capacity]", Map);

    public static SharingType? Get(int sharingTypeId) =>
        Db.Query("SELECT * FROM [SharingType] WHERE [SharingTypeId] = ?", Map,
            Db.Param("@SharingTypeId", sharingTypeId)).FirstOrDefault();

    public static int UpdateRent(int sharingTypeId, decimal rent) =>
        Db.Execute("UPDATE [SharingType] SET [Rent] = ? WHERE [SharingTypeId] = ?",
            Db.Param("@Rent", rent),
            Db.Param("@SharingTypeId", sharingTypeId));

    private static SharingType Map(IDataRecord record) => new()
    {
        SharingTypeId = record.GetInt("SharingTypeId"),
        SharingName = record.GetText("SharingName"),
        Capacity = record.GetInt("Capacity"),
        Rent = record.GetMoney("Rent"),
    };
}
