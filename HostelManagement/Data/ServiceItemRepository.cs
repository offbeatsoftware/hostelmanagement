using System.Data;
using System.Data.OleDb;
using HostelManagement.Models;

namespace HostelManagement.Data;

public static class ServiceItemRepository
{
    // StudentCount = students using the service now (no end date).
    private const string SelectServices =
        "SELECT v.*, (SELECT COUNT(*) FROM [StudentService] AS u WHERE u.[ServiceId] = v.[ServiceId] " +
        "AND u.[EndDate] IS NULL) AS [StudentCount] FROM [Service] AS v";

    public static List<ServiceItem> GetForHostel(int hostelId) =>
        Db.Query(SelectServices + " WHERE v.[HostelId] = ? ORDER BY v.[ServiceName]", Map, Db.Param("@HostelId", hostelId));

    public static ServiceItem? Get(int serviceId) =>
        Db.Query(SelectServices + " WHERE v.[ServiceId] = ?", Map, Db.Param("@ServiceId", serviceId)).FirstOrDefault();

    public static int Insert(ServiceItem service) =>
        Db.Insert(
            "INSERT INTO [Service] ([HostelId], [ServiceName], [IsIncludedInRent], [MonthlyRate], [IsActive]) VALUES (?, ?, ?, ?, ?)",
            Db.Param("@HostelId", service.HostelId),
            Db.Param("@ServiceName", service.ServiceName),
            Db.Param("@IsIncludedInRent", service.IsIncludedInRent),
            Db.Param("@MonthlyRate", service.MonthlyRate),
            Db.Param("@IsActive", service.IsActive));

    public static int Update(ServiceItem service) =>
        Db.Execute(
            "UPDATE [Service] SET [ServiceName] = ?, [IsIncludedInRent] = ?, [MonthlyRate] = ?, [IsActive] = ? WHERE [ServiceId] = ?",
            Db.Param("@ServiceName", service.ServiceName),
            Db.Param("@IsIncludedInRent", service.IsIncludedInRent),
            Db.Param("@MonthlyRate", service.MonthlyRate),
            Db.Param("@IsActive", service.IsActive),
            Db.Param("@ServiceId", service.ServiceId));

    public static int Delete(int serviceId) =>
        Db.Execute("DELETE FROM [Service] WHERE [ServiceId] = ?", Db.Param("@ServiceId", serviceId));

    /// <summary>Adds Wi-Fi and laundry (included in rent) and transport (extra, rate 0) for a new hostel.</summary>
    public static void InsertDefaults(OleDbConnection connection, OleDbTransaction transaction, int hostelId)
    {
        foreach ((string name, bool includedInRent) in DatabaseSchema.DefaultServices)
        {
            Db.Execute(connection, transaction,
                "INSERT INTO [Service] ([HostelId], [ServiceName], [IsIncludedInRent], [MonthlyRate], [IsActive]) VALUES (?, ?, ?, ?, ?)",
                Db.Param("@HostelId", hostelId),
                Db.Param("@ServiceName", name),
                Db.Param("@IsIncludedInRent", includedInRent),
                Db.Param("@MonthlyRate", 0m),
                Db.Param("@IsActive", true));
        }
    }

    public static void DeleteForHostel(OleDbConnection connection, OleDbTransaction transaction, int hostelId) =>
        Db.Execute(connection, transaction, "DELETE FROM [Service] WHERE [HostelId] = ?", Db.Param("@HostelId", hostelId));

    /// <summary>True when another service of the hostel already has this name (ignoring case).</summary>
    public static bool NameExists(int hostelId, string serviceName, int exceptServiceId) =>
        Convert.ToInt32(Db.Scalar(
            "SELECT COUNT(*) FROM [Service] WHERE [HostelId] = ? AND UCASE([ServiceName]) = UCASE(?) AND [ServiceId] <> ?",
            Db.Param("@HostelId", hostelId),
            Db.Param("@ServiceName", serviceName),
            Db.Param("@ServiceId", exceptServiceId))) > 0;

    /// <summary>All uses of the service by students, past and present.</summary>
    public static int CountAllUses(int serviceId) =>
        Convert.ToInt32(Db.Scalar("SELECT COUNT(*) FROM [StudentService] WHERE [ServiceId] = ?",
            Db.Param("@ServiceId", serviceId)));

    private static ServiceItem Map(IDataRecord record) => new()
    {
        ServiceId = record.GetInt("ServiceId"),
        HostelId = record.GetInt("HostelId"),
        ServiceName = record.GetText("ServiceName"),
        IsIncludedInRent = record.GetBool("IsIncludedInRent"),
        MonthlyRate = record.GetMoney("MonthlyRate"),
        IsActive = record.GetBool("IsActive"),
        StudentCount = record.GetInt("StudentCount"),
    };
}
