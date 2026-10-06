namespace HostelManagement.Models;

/// <summary>Why a backup was made. Only automatic backups are ever deleted (the oldest beyond 30).</summary>
public static class BackupKind
{
    public const string Manual = "Manual";
    public const string Automatic = "Automatic";
    public const string BeforeRestore = "BeforeRestore";

    public static string DisplayName(string kind) => kind switch
    {
        Automatic => "Automatic (daily)",
        BeforeRestore => "Before a restore",
        _ => "Backup Now",
    };
}

/// <summary>A backup file in the Backups folder.</summary>
public sealed record BackupFile(string FilePath, DateTime Created, string Kind, long SizeBytes)
{
    public string FileName => Path.GetFileName(FilePath);
    public string KindText => BackupKind.DisplayName(Kind);
    public string SizeText => SizeBytes >= 1024 * 1024 ? $"{SizeBytes / 1024d / 1024d:0.0} MB" : $"{Math.Max(1, SizeBytes / 1024)} KB";
}

/// <summary>Result of making a backup; the second copy may fail (for example a USB drive that is not plugged in).</summary>
public sealed record BackupResult(BackupFile File, string? SecondCopyProblem);

/// <summary>What a backup file contains, checked before it is restored.</summary>
public sealed record BackupDetails(string FilePath, DateTime Created, string ApplicationVersion, int SchemaVersion, int FileCount);
