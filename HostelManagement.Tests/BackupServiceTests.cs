using System.IO.Compression;
using HostelManagement.Data;
using HostelManagement.Models;
using HostelManagement.Services;
using HostelManagement.Utilities;
using Xunit;

namespace HostelManagement.Tests;

public sealed class BackupServiceTests : TestDatabase
{
    private static readonly DateTime Now = new(2026, 10, 6, 18, 30, 0);

    private string PhotoPath(string name) => Path.Combine(DataFolder, "Photos", "Students", name);

    private static List<string> HostelNames() =>
        HostelService.GetHostels().Select(h => h.HostelName).OrderBy(n => n).ToList();

    [Fact]
    public void Create_ZipsTheDatabasePhotosAndDocuments()
    {
        AddHostel("Boys Hostel");
        File.WriteAllText(PhotoPath("aman.jpg"), "photo");
        File.WriteAllText(Path.Combine(DataFolder, "Documents", "Students", "aman_aadhaar.pdf"), "card");

        BackupResult result = BackupService.Create(BackupKind.Manual, Now);

        Assert.Null(result.SecondCopyProblem);
        Assert.Equal("HostelBackup_2026-10-06_183000_Manual.zip", result.File.FileName);
        Assert.Equal(Path.Combine(DataFolder, "Backups", result.File.FileName), result.File.FilePath);
        using ZipArchive zip = ZipFile.OpenRead(result.File.FilePath);
        List<string> entries = zip.Entries.Select(e => e.FullName).ToList();
        Assert.Contains("Database/HostelManagement.accdb", entries);
        Assert.Contains("Photos/Students/aman.jpg", entries);
        Assert.Contains("Documents/Students/aman_aadhaar.pdf", entries);
        Assert.Contains("backup-info.txt", entries);

        BackupFile listed = BackupService.GetBackups().Single();
        Assert.Equal(Now, listed.Created);
        Assert.Equal(BackupKind.Manual, listed.Kind);
    }

    [Fact]
    public void Create_SameSecondTwice_KeepsBothFiles()
    {
        BackupService.Create(BackupKind.Manual, Now);
        BackupService.Create(BackupKind.Manual, Now);

        Assert.Equal(2, BackupService.GetBackups().Count);
    }

    [Fact]
    public void AutomaticBackup_IsMadeOnceADay()
    {
        BackupService.AutomaticBackupEnabled = true;
        try
        {
            Assert.NotNull(BackupService.RunAutomaticIfDue(Now));
            Assert.Null(BackupService.RunAutomaticIfDue(Now.AddHours(2)));
            Assert.NotNull(BackupService.RunAutomaticIfDue(Now.AddDays(1)));
            Assert.Equal(2, BackupService.GetBackups().Count(b => b.Kind == BackupKind.Automatic));
        }
        finally
        {
            BackupService.AutomaticBackupEnabled = false;
        }
    }

    [Fact]
    public void OnlyTheLast30AutomaticBackupsAreKept_ManualOnesAreNeverDeleted()
    {
        string folder = Path.Combine(DataFolder, "Backups");
        for (int day = 1; day <= 35; day++)
        {
            File.WriteAllText(Path.Combine(folder, $"HostelBackup_2026-08-{day % 31 + 1:00}_0{day / 31}0000_Automatic.zip"), "old");
        }
        File.WriteAllText(Path.Combine(folder, "HostelBackup_2026-01-01_100000_Manual.zip"), "manual");

        BackupService.Create(BackupKind.Automatic, Now);

        List<BackupFile> backups = BackupService.GetBackups();
        Assert.Equal(BackupService.AutomaticBackupsToKeep, backups.Count(b => b.Kind == BackupKind.Automatic));
        Assert.Contains(backups, b => b.Created == Now);
        Assert.Contains(backups, b => b.Kind == BackupKind.Manual);
    }

    [Fact]
    public void SecondFolder_GetsACopy_AndAProblemIsReportedWhenItIsMissing()
    {
        string second = Path.Combine(DataFolder, "UsbDrive");
        Directory.CreateDirectory(second);
        BackupService.SetSecondFolder(second);

        BackupResult copied = BackupService.Create(BackupKind.Manual, Now);
        Assert.Null(copied.SecondCopyProblem);
        Assert.True(File.Exists(Path.Combine(second, copied.File.FileName)));
        Assert.Equal(second, BackupService.GetSecondFolder());

        Directory.Delete(second, recursive: true);
        BackupResult notCopied = BackupService.Create(BackupKind.Manual, Now.AddMinutes(1));
        Assert.Contains("not available", notCopied.SecondCopyProblem);
        Assert.True(File.Exists(notCopied.File.FilePath));
    }

    [Fact]
    public void SecondFolder_Rules()
    {
        Assert.Contains("does not exist", Assert.Throws<ValidationException>(
            () => BackupService.SetSecondFolder(Path.Combine(DataFolder, "Missing"))).Message);
        Assert.Contains("other than", Assert.Throws<ValidationException>(
            () => BackupService.SetSecondFolder(AppPaths.BackupsFolder)).Message);

        BackupService.SetSecondFolder("");
        Assert.Equal("", BackupService.GetSecondFolder());
    }

    [Fact]
    public void Inspect_RejectsFilesThatAreNotBackups()
    {
        string notZip = Path.Combine(DataFolder, "notes.zip");
        File.WriteAllText(notZip, "not a zip");
        Assert.Contains("not a valid backup", Assert.Throws<ValidationException>(() => BackupService.Inspect(notZip)).Message);

        string emptyZip = Path.Combine(DataFolder, "empty.zip");
        using (ZipFile.Open(emptyZip, ZipArchiveMode.Create))
        {
        }
        Assert.Contains("no database", Assert.Throws<ValidationException>(() => BackupService.Inspect(emptyZip)).Message);

        Assert.Contains("not found", Assert.Throws<ValidationException>(
            () => BackupService.Inspect(Path.Combine(DataFolder, "missing.zip"))).Message);
    }

    [Fact]
    public void Inspect_RejectsABackupFromAnOlderVersion()
    {
        Db.Execute("UPDATE [SchemaInfo] SET [Version] = ?", Db.Param("@Version", DatabaseSchema.Version - 1));
        BackupResult old = BackupService.Create(BackupKind.Manual, Now);
        Db.Execute("UPDATE [SchemaInfo] SET [Version] = ?", Db.Param("@Version", DatabaseSchema.Version));

        Assert.Contains("older version", Assert.Throws<ValidationException>(() => BackupService.Inspect(old.File.FilePath)).Message);
    }

    [Fact]
    public void Inspect_ReadsTheBackupDetails()
    {
        File.WriteAllText(PhotoPath("aman.jpg"), "photo");
        BackupResult backup = BackupService.Create(BackupKind.Manual, Now);

        BackupDetails details = BackupService.Inspect(backup.File.FilePath);

        Assert.Equal(Now, details.Created);
        Assert.Equal(DatabaseSchema.Version, details.SchemaVersion);
        Assert.Equal(1, details.FileCount);
    }

    [Fact]
    public void Restore_ReplacesDataAndFiles_AfterSavingTheCurrentData()
    {
        AddHostel("Boys Hostel");
        File.WriteAllText(PhotoPath("aman.jpg"), "photo");
        BackupResult backup = BackupService.Create(BackupKind.Manual, Now);

        AddHostel("Girls Hostel");
        File.Delete(PhotoPath("aman.jpg"));
        File.WriteAllText(PhotoPath("priya.jpg"), "photo");

        BackupResult safety = BackupService.Restore(backup.File.FilePath, Now.AddHours(1));

        Assert.Equal(["Boys Hostel"], HostelNames());
        Assert.True(File.Exists(PhotoPath("aman.jpg")));
        Assert.False(File.Exists(PhotoPath("priya.jpg")));
        Assert.True(Directory.Exists(Path.Combine(DataFolder, "Documents", "Students")));

        // The data from before the restore was saved and can be restored again.
        Assert.Equal(BackupKind.BeforeRestore, safety.File.Kind);
        BackupService.Restore(safety.File.FilePath, Now.AddHours(2));
        Assert.Equal(["Boys Hostel", "Girls Hostel"], HostelNames());
        Assert.True(File.Exists(PhotoPath("priya.jpg")));
    }
}
