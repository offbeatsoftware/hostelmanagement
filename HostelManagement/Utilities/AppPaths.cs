namespace HostelManagement.Utilities;

/// <summary>
/// Central place for every folder and file location used by the application.
/// All data (database, photos, documents, backups, logs) is kept together in
/// one data folder, separate from the program files, so the application can
/// be installed under Program Files and the data is easy to find and back up.
/// </summary>
public static class AppPaths
{
    public const string DefaultDataFolder = @"C:\HostelData";

    public static string DataFolder { get; private set; } = DefaultDataFolder;

    /// <summary>Points the application at another data folder. Used by the automated tests.</summary>
    internal static void UseDataFolder(string folder) => DataFolder = folder;

    public static string DatabaseFolder => Path.Combine(DataFolder, "Database");
    public static string DatabaseFile => Path.Combine(DatabaseFolder, "HostelManagement.accdb");

    /// <summary>Empty database shipped with the application; copied to the data folder on first start.</summary>
    public static string DatabaseTemplateFile =>
        Path.Combine(AppContext.BaseDirectory, "Database", "HostelManagement.accdb");

    public static string StudentPhotosFolder => Path.Combine(DataFolder, "Photos", "Students");
    public static string StudentDocumentsFolder => Path.Combine(DataFolder, "Documents", "Students");

    public static string BackupsFolder => Path.Combine(DataFolder, "Backups");
    public static string LogsFolder => Path.Combine(DataFolder, "Logs");

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
