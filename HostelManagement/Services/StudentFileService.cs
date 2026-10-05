using HostelManagement.Utilities;

namespace HostelManagement.Services;

/// <summary>
/// Student photos and scanned Aadhaar cards. Files are copied into the application's
/// Photos and Documents folders under a generated name (the original name is never used),
/// and only the relative path is stored in the database.
/// </summary>
public static class StudentFileService
{
    public const long MaxFileBytes = 5 * 1024 * 1024;

    public static IReadOnlyList<string> PhotoExtensions { get; } = [".jpg", ".jpeg", ".png", ".bmp"];
    public static IReadOnlyList<string> AadhaarCardExtensions { get; } = [".jpg", ".jpeg", ".png", ".pdf"];

    public const string PhotoFileFilter = "Photos (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp";
    public const string AadhaarCardFileFilter = "Aadhaar card (*.pdf;*.jpg;*.jpeg;*.png)|*.pdf;*.jpg;*.jpeg;*.png";

    /// <summary>Checks a photo chosen by the admin before anything is saved.</summary>
    public static void ValidatePhoto(string sourceFile)
    {
        ValidateFile(sourceFile, PhotoExtensions, "photo");
        try
        {
            using var stream = File.OpenRead(sourceFile);
            using var image = Image.FromStream(stream);
        }
        catch (Exception ex) when (ex is ArgumentException or OutOfMemoryException or IOException)
        {
            throw new ValidationException("The selected photo could not be read. Please choose a JPG or PNG picture.");
        }
    }

    /// <summary>Checks a scanned Aadhaar card chosen by the admin before anything is saved.</summary>
    public static void ValidateAadhaarCard(string sourceFile)
    {
        ValidateFile(sourceFile, AadhaarCardExtensions, "Aadhaar card file");

        if (Path.GetExtension(sourceFile).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            var header = new byte[4];
            using var stream = File.OpenRead(sourceFile);
            if (stream.Read(header, 0, 4) < 4 || header[0] != '%' || header[1] != 'P' || header[2] != 'D' || header[3] != 'F')
            {
                throw new ValidationException("The selected Aadhaar card file is not a valid PDF.");
            }
        }
    }

    /// <summary>Copies a validated photo for the student and returns its relative path.</summary>
    public static string StorePhoto(int studentId, string sourceFile) =>
        Store(sourceFile, AppPaths.StudentPhotosFolder, $"S{studentId}_photo");

    /// <summary>Copies a validated Aadhaar card file for the student and returns its relative path.</summary>
    public static string StoreAadhaarCard(int studentId, string sourceFile) =>
        Store(sourceFile, AppPaths.StudentDocumentsFolder, $"S{studentId}_aadhaar");

    /// <summary>Full path of a stored file.</summary>
    public static string FullPath(string relativePath) => Path.Combine(AppPaths.DataFolder, relativePath);

    /// <summary>True when the path is set and the file is still on disk.</summary>
    public static bool Exists(string relativePath) =>
        relativePath.Length > 0 && File.Exists(FullPath(relativePath));

    /// <summary>Deletes a stored file; a missing or locked file is only logged.</summary>
    public static void TryDelete(string relativePath)
    {
        if (relativePath.Length == 0)
        {
            return;
        }

        try
        {
            File.Delete(FullPath(relativePath));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLogger.Error($"Could not delete the student file {relativePath}.", ex);
        }
    }

    /// <summary>Loads a stored photo without keeping the file locked; null when missing or unreadable.</summary>
    public static Image? LoadPhoto(string relativePath)
    {
        if (!Exists(relativePath))
        {
            return null;
        }

        try
        {
            using var stream = new MemoryStream(File.ReadAllBytes(FullPath(relativePath)));
            using var image = Image.FromStream(stream);
            return new Bitmap(image);
        }
        catch (Exception ex) when (ex is ArgumentException or OutOfMemoryException or IOException)
        {
            AppLogger.Error($"Could not load the photo {relativePath}.", ex);
            return null;
        }
    }

    private static void ValidateFile(string sourceFile, IReadOnlyList<string> allowedExtensions, string description)
    {
        if (!File.Exists(sourceFile))
        {
            throw new ValidationException($"The selected {description} could not be found.");
        }

        string extension = Path.GetExtension(sourceFile);
        if (!allowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            throw new ValidationException(
                $"The {description} must be one of these file types: {string.Join(", ", allowedExtensions)}.");
        }

        long size = new FileInfo(sourceFile).Length;
        if (size == 0)
        {
            throw new ValidationException($"The selected {description} is empty.");
        }
        if (size > MaxFileBytes)
        {
            throw new ValidationException($"The {description} is too large. The maximum size is 5 MB.");
        }
    }

    private static string Store(string sourceFile, string folder, string namePrefix)
    {
        Directory.CreateDirectory(folder);
        string fileName = $"{namePrefix}_{DateTime.Now:yyyyMMddHHmmssfff}{Path.GetExtension(sourceFile).ToLowerInvariant()}";
        string target = Path.Combine(folder, fileName);
        File.Copy(sourceFile, target);
        return Path.GetRelativePath(AppPaths.DataFolder, target);
    }
}
