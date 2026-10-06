using System.Data.OleDb;
using System.Globalization;
using System.IO.Compression;
using HostelManagement.Data;
using HostelManagement.Models;
using HostelManagement.Utilities;

namespace HostelManagement.Services;

/// <summary>
/// Backup and restore (client decisions, Phase 14): a backup is one zip file with the database and the
/// student photos and documents. An automatic backup is made once a day when the application is closed;
/// the last 30 automatic backups are kept, manual backups are never deleted. Each backup can also be copied
/// to a second folder (a USB drive, or a Google Drive or OneDrive folder). Restoring asks for confirmation,
/// first saves a backup of the current data, then replaces the database, photos and documents.
/// </summary>
public static class BackupService
{
    public const int AutomaticBackupsToKeep = 30;
    private const string FilePrefix = "HostelBackup_";
    private const string DatabaseEntry = "Database/HostelManagement.accdb";
    private const string InfoEntry = "backup-info.txt";
    private const string SecondFolderSetting = "Backup.SecondFolder";

    /// <summary>The student file folders (relative to the data folder) that are backed up with the database.</summary>
    private static readonly string[] FileFolders = ["Photos", "Documents"];

    /// <summary>Turned off by the automated tests, which open and close the main window many times.</summary>
    public static bool AutomaticBackupEnabled { get; set; } = true;

    /// <summary>Set after a restore so that closing the application does not back up again.</summary>
    public static bool SkipAutomaticBackupOnExit { get; set; }

    public static List<BackupFile> GetBackups() => GetBackups(AppPaths.BackupsFolder);

    public static string GetSecondFolder() =>
        AppSettingRepository.GetAll().GetValueOrDefault(SecondFolderSetting, string.Empty);

    /// <summary>Sets the second backup folder after checking that files can be written there; empty turns it off.</summary>
    public static void SetSecondFolder(string folder)
    {
        folder = Validators.Clean(folder);
        if (folder.Length > 0)
        {
            if (!Directory.Exists(folder))
            {
                throw new ValidationException($"The folder {folder} does not exist. Plug in the drive or choose another folder.");
            }
            if (IsSameFolder(folder, AppPaths.BackupsFolder))
            {
                throw new ValidationException("Choose a folder other than the application's Backups folder.");
            }
            string testFile = Path.Combine(folder, $".hostel-write-test-{Guid.NewGuid():N}");
            try
            {
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new ValidationException($"Backups cannot be saved in {folder}. Choose a folder you can write to.");
            }
        }
        AppSettingRepository.SaveAll(new Dictionary<string, string> { [SecondFolderSetting] = folder });
    }

    /// <summary>True when no automatic backup has been made today.</summary>
    public static bool IsAutomaticBackupDue(DateTime now) =>
        !GetBackups().Any(b => b.Kind == BackupKind.Automatic && b.Created.Date == now.Date);

    /// <summary>Called when the application closes: makes the day's automatic backup if it has not been made yet.</summary>
    public static BackupResult? RunAutomaticIfDue(DateTime now) =>
        AutomaticBackupEnabled && !SkipAutomaticBackupOnExit && Db.IsConfigured && IsAutomaticBackupDue(now)
            ? Create(BackupKind.Automatic, now)
            : null;

    /// <summary>Makes a backup zip in the Backups folder and, if set, copies it to the second folder.</summary>
    public static BackupResult Create(string kind, DateTime now)
    {
        Directory.CreateDirectory(AppPaths.BackupsFolder);
        string fileName = $"{FilePrefix}{now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture)}_{kind}.zip";
        string target = Path.Combine(AppPaths.BackupsFolder, fileName);
        for (int i = 2; File.Exists(target); i++)
        {
            target = Path.Combine(AppPaths.BackupsFolder, fileName.Replace(".zip", $"_{i}.zip", StringComparison.Ordinal));
        }

        string temp = Path.Combine(Path.GetTempPath(), $"hostel-backup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temp);
        try
        {
            // Copy the database first: pooled connections are released so that its contents are complete.
            ReleaseDatabaseConnections();
            string databaseCopy = Path.Combine(temp, "HostelManagement.accdb");
            using (var source = new FileStream(AppPaths.DatabaseFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var copy = File.Create(databaseCopy))
            {
                source.CopyTo(copy);
            }

            string zipTemp = Path.Combine(temp, "backup.zip");
            int fileCount = 0;
            using (ZipArchive zip = ZipFile.Open(zipTemp, ZipArchiveMode.Create))
            {
                zip.CreateEntryFromFile(databaseCopy, DatabaseEntry, CompressionLevel.Optimal);
                foreach (string folder in FileFolders)
                {
                    string fullFolder = Path.Combine(AppPaths.DataFolder, folder);
                    if (!Directory.Exists(fullFolder))
                    {
                        continue;
                    }
                    foreach (string file in Directory.EnumerateFiles(fullFolder, "*", SearchOption.AllDirectories))
                    {
                        string entry = Path.GetRelativePath(AppPaths.DataFolder, file).Replace('\\', '/');
                        zip.CreateEntryFromFile(file, entry, CompressionLevel.Optimal);
                        fileCount++;
                    }
                }

                ZipArchiveEntry info = zip.CreateEntry(InfoEntry);
                using var writer = new StreamWriter(info.Open());
                writer.WriteLine($"Application={AppInfo.ProductName}");
                writer.WriteLine($"ApplicationVersion={AppInfo.Version}");
                writer.WriteLine($"SchemaVersion={DatabaseSchema.Version}");
                writer.WriteLine($"Created={now.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)}");
                writer.WriteLine($"Kind={kind}");
                writer.WriteLine($"Files={fileCount}");
            }
            File.Move(zipTemp, target);
        }
        finally
        {
            TryDeleteFolder(temp);
        }

        BackupFile backup = ToBackupFile(target)!;
        AppLogger.Info($"Backup {backup.FileName} created ({backup.SizeText}).");

        string? secondCopyProblem = CopyToSecondFolder(backup);
        DeleteOldAutomaticBackups(AppPaths.BackupsFolder);
        return new BackupResult(backup, secondCopyProblem);
    }

    /// <summary>Checks that the file is a backup of this application that this version can restore.</summary>
    public static BackupDetails Inspect(string zipPath)
    {
        if (!File.Exists(zipPath))
        {
            throw new ValidationException("The backup file was not found.");
        }

        string temp = Path.Combine(Path.GetTempPath(), $"hostel-check-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temp);
        try
        {
            Dictionary<string, string> info;
            int fileCount;
            string databaseCopy = Path.Combine(temp, "check.accdb");
            try
            {
                using ZipArchive zip = ZipFile.OpenRead(zipPath);
                ZipArchiveEntry database = zip.GetEntry(DatabaseEntry)
                    ?? throw new ValidationException("This file is not a backup of the Hostel Management System (no database inside).");
                database.ExtractToFile(databaseCopy);
                info = ReadInfo(zip);
                fileCount = zip.Entries.Count(e => FileFolders.Any(f => e.FullName.StartsWith(f + "/", StringComparison.OrdinalIgnoreCase)) && e.Name.Length > 0);
            }
            catch (InvalidDataException)
            {
                throw new ValidationException("This file is not a valid backup (it is not a zip file or it is damaged).");
            }

            int schemaVersion = ReadSchemaVersion(databaseCopy);
            if (schemaVersion != DatabaseSchema.Version)
            {
                throw new ValidationException(schemaVersion < DatabaseSchema.Version
                    ? "This backup was made by an older version of the application and cannot be restored by this version."
                    : "This backup was made by a newer version of the application. Update the application first.");
            }

            DateTime created = DateTime.TryParseExact(info.GetValueOrDefault("Created"), "yyyy-MM-ddTHH:mm:ss",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed) ? parsed : File.GetLastWriteTime(zipPath);
            return new BackupDetails(zipPath, created, info.GetValueOrDefault("ApplicationVersion", ""), schemaVersion, fileCount);
        }
        finally
        {
            TryDeleteFolder(temp);
        }
    }

    /// <summary>
    /// Replaces the current database, photos and documents with those in the backup, after saving a backup
    /// of the current data. Returns that safety backup. The application must be restarted afterwards.
    /// </summary>
    public static BackupResult Restore(string zipPath, DateTime now)
    {
        Inspect(zipPath);
        BackupResult safety = Create(BackupKind.BeforeRestore, now);

        string temp = Path.Combine(Path.GetTempPath(), $"hostel-restore-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temp);
        try
        {
            using ZipArchive zip = ZipFile.OpenRead(zipPath);

            // Extract everything to a temporary folder first, so a damaged zip changes nothing.
            foreach (ZipArchiveEntry entry in zip.Entries.Where(e => e.Name.Length > 0 && e.FullName != InfoEntry))
            {
                string destination = Path.GetFullPath(Path.Combine(temp, entry.FullName));
                if (!destination.StartsWith(Path.GetFullPath(temp) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ValidationException("The backup contains an unexpected file path and was not restored.");
                }
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                entry.ExtractToFile(destination);
            }

            ReleaseDatabaseConnections();
            File.Copy(Path.Combine(temp, "Database", "HostelManagement.accdb"), AppPaths.DatabaseFile, overwrite: true);
            foreach (string folder in FileFolders)
            {
                string current = Path.Combine(AppPaths.DataFolder, folder);
                if (Directory.Exists(current))
                {
                    Directory.Delete(current, recursive: true);
                }
                string restored = Path.Combine(temp, folder);
                if (Directory.Exists(restored))
                {
                    CopyFolder(restored, current);
                }
            }
            AppPaths.EnsureFolders();
        }
        finally
        {
            TryDeleteFolder(temp);
        }

        AppLogger.Info($"Restored backup {Path.GetFileName(zipPath)}; the previous data was saved as {safety.File.FileName}.");
        return safety;
    }

    private static List<BackupFile> GetBackups(string folder) =>
        Directory.Exists(folder)
            ? Directory.EnumerateFiles(folder, FilePrefix + "*.zip")
                .Select(ToBackupFile)
                .OfType<BackupFile>()
                .OrderByDescending(b => b.Created)
                .ThenByDescending(b => b.FileName, StringComparer.Ordinal)
                .ToList()
            : [];

    /// <summary>Reads the date and kind from a name such as HostelBackup_2026-10-06_183012_Automatic.zip.</summary>
    private static BackupFile? ToBackupFile(string path)
    {
        string[] parts = Path.GetFileNameWithoutExtension(path)[FilePrefix.Length..].Split('_');
        if (parts.Length < 3 || !DateTime.TryParseExact($"{parts[0]}_{parts[1]}", "yyyy-MM-dd_HHmmss",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime created))
        {
            return null;
        }
        return new BackupFile(path, created, parts[2], new FileInfo(path).Length);
    }

    private static string? CopyToSecondFolder(BackupFile backup)
    {
        string folder = GetSecondFolder();
        if (folder.Length == 0)
        {
            return null;
        }
        try
        {
            if (!Directory.Exists(folder))
            {
                return $"The second backup folder {folder} is not available (is the drive plugged in?). The backup is only in the Backups folder.";
            }
            File.Copy(backup.FilePath, Path.Combine(folder, backup.FileName), overwrite: true);
            DeleteOldAutomaticBackups(folder);
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLogger.Error($"Could not copy backup {backup.FileName} to {folder}.", ex);
            return $"The backup could not be copied to {folder}. It is only in the Backups folder.";
        }
    }

    private static void DeleteOldAutomaticBackups(string folder)
    {
        foreach (BackupFile old in GetBackups(folder).Where(b => b.Kind == BackupKind.Automatic).Skip(AutomaticBackupsToKeep))
        {
            try
            {
                File.Delete(old.FilePath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                AppLogger.Error($"Could not delete the old backup {old.FilePath}.", ex);
            }
        }
    }

    private static Dictionary<string, string> ReadInfo(ZipArchive zip)
    {
        var info = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        ZipArchiveEntry? entry = zip.GetEntry(InfoEntry);
        if (entry is null)
        {
            return info;
        }
        using var reader = new StreamReader(entry.Open());
        while (reader.ReadLine() is string line)
        {
            int separator = line.IndexOf('=');
            if (separator > 0)
            {
                info[line[..separator]] = line[(separator + 1)..];
            }
        }
        return info;
    }

    /// <summary>Opens the database from the backup on its own (not through <see cref="Db"/>) and reads its schema version.</summary>
    private static int ReadSchemaVersion(string databaseFile)
    {
        var builder = new OleDbConnectionStringBuilder(Db.BuildConnectionString(Db.ProviderName, databaseFile))
        {
            ["OLE DB Services"] = -4, // no pooling, so the temporary file is released at once
        };
        try
        {
            using var connection = new OleDbConnection(builder.ConnectionString);
            connection.Open();
            if (!DatabaseInitializer.GetExistingTableNames(connection).Contains("SchemaInfo"))
            {
                return 0;
            }
            using var command = new OleDbCommand("SELECT MAX([Version]) FROM [SchemaInfo]", connection);
            object? value = command.ExecuteScalar();
            return value is null or DBNull ? 0 : Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }
        catch (OleDbException ex)
        {
            AppLogger.Error("The database in the backup could not be opened.", ex);
            throw new ValidationException("The database in this backup could not be opened. The file may be damaged.");
        }
    }

    /// <summary>Closes pooled connections so that the database file can be copied or replaced.</summary>
    private static void ReleaseDatabaseConnections()
    {
        OleDbConnection.ReleaseObjectPool();
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }

    private static void CopyFolder(string source, string destination)
    {
        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            string target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    private static bool IsSameFolder(string a, string b) =>
        string.Equals(Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar), Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);

    private static void TryDeleteFolder(string folder)
    {
        try
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLogger.Error($"Could not delete the temporary folder {folder}.", ex);
        }
    }
}
