namespace HostelManagement.Utilities;

/// <summary>
/// Central place for every folder and file location used by the application.
/// All data (database, photos, documents, backups, logs) is kept in folders next to
/// HostelManagement.exe, so it is part of the installed application folder.
/// </summary>
public static class AppPaths
{
    public static string DataFolder { get; private set; } = AppContext.BaseDirectory;

    /// <summary>Points the application at another data folder. Used by the automated tests.</summary>
    internal static void UseDataFolder(string folder) => DataFolder = folder;

    public static string DatabaseFolder => Path.Combine(DataFolder, "Database");
    public static string DatabaseFile => Path.Combine(DatabaseFolder, "HostelManagement.accdb");

    public static string StudentPhotosFolder => Path.Combine(DataFolder, "Photos", "Students");
    public static string StudentDocumentsFolder => Path.Combine(DataFolder, "Documents", "Students");

    public static string BackupsFolder => Path.Combine(DataFolder, "Backups");
    public static string InvoicesFolder => Path.Combine(DataFolder, "Invoices");
    public static string LogsFolder => Path.Combine(DataFolder, "Logs");

    /// <summary>Creates any missing application folders. Safe to call on every start.</summary>
    public static void EnsureFolders()
    {
        Directory.CreateDirectory(DatabaseFolder);
        Directory.CreateDirectory(StudentPhotosFolder);
        Directory.CreateDirectory(StudentDocumentsFolder);
        Directory.CreateDirectory(BackupsFolder);
        Directory.CreateDirectory(InvoicesFolder);
        Directory.CreateDirectory(LogsFolder);
    }
}
