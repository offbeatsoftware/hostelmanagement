namespace HostelManagement.Utilities;

/// <summary>
/// Central place for every folder and file location used by the application.
/// All data (database, photos, documents, backups, logs) is kept together in
/// one data folder, separate from the program files, so the application can
/// be installed under Program Files and the data is easy to find and back up.
/// </summary>
public static class AppPaths
{
    public const string DataFolder = @"C:\HostelData";

    public static string DatabaseFolder => Path.Combine(DataFolder, "Database");
    public static string DatabaseFile => Path.Combine(DatabaseFolder, "HostelManagement.accdb");

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
