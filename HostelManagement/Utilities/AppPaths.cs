namespace HostelManagement.Utilities;

/// <summary>
/// Central place for every folder and file location used by the application.
/// All locations are relative to the application folder so the database,
/// photos, documents and backups stay together on the hostel PC.
/// </summary>
public static class AppPaths
{
    public static string BaseFolder { get; } = AppContext.BaseDirectory;

    public static string DatabaseFolder => Path.Combine(BaseFolder, "Database");
    public static string DatabaseFile => Path.Combine(DatabaseFolder, "HostelManagement.accdb");

    public static string StudentPhotosFolder => Path.Combine(BaseFolder, "Photos", "Students");
    public static string StudentDocumentsFolder => Path.Combine(BaseFolder, "Documents", "Students");

    public static string BackupsFolder => Path.Combine(BaseFolder, "Backups");
    public static string LogsFolder => Path.Combine(BaseFolder, "Logs");

    /// <summary>Creates any missing application folders. Safe to call on every start.</summary>
    public static void EnsureFolders()
    {
        Directory.CreateDirectory(DatabaseFolder);
        Directory.CreateDirectory(StudentPhotosFolder);
        Directory.CreateDirectory(StudentDocumentsFolder);
        Directory.CreateDirectory(BackupsFolder);
        Directory.CreateDirectory(LogsFolder);
    }
}
