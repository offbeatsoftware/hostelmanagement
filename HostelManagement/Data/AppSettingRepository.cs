using HostelManagement.Utilities;

namespace HostelManagement.Data;

/// <summary>Application settings stored as key and value pairs (email account and email texts).</summary>
public static class AppSettingRepository
{
    public static Dictionary<string, string> GetAll() =>
        Db.Query("SELECT [SettingKey], [SettingValue] FROM [AppSetting]",
                r => (Key: r.GetText("SettingKey"), Value: r.GetText("SettingValue")))
            .ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);

    /// <summary>Saves all values in one transaction, adding keys that do not exist yet.</summary>
    public static void SaveAll(IReadOnlyDictionary<string, string> values) =>
        Db.InTransaction((connection, transaction) =>
        {
            foreach ((string key, string value) in values)
            {
                int updated = Db.Execute(connection, transaction,
                    "UPDATE [AppSetting] SET [SettingValue] = ? WHERE [SettingKey] = ?",
                    Db.OptionalText("@SettingValue", value),
                    Db.Param("@SettingKey", key));
                if (updated == 0)
                {
                    Db.Execute(connection, transaction,
                        "INSERT INTO [AppSetting] ([SettingKey], [SettingValue]) VALUES (?, ?)",
                        Db.Param("@SettingKey", key),
                        Db.OptionalText("@SettingValue", value));
                }
            }
        });
}
