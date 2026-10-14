using System.Drawing.Imaging;
using System.Globalization;
using HostelManagement.Utilities;

namespace HostelManagement.Services;

/// <summary>Who signs the residency agreement, in the order of the signature lines.</summary>
public enum AgreementSigner
{
    Owner,
    Student,
    Parent,
    Witness,
}

/// <summary>
/// The signatures of one student's agreement: full paths of PNG images (null when that person has not signed),
/// the witness's name and the date the agreement was signed.
/// </summary>
public sealed record AgreementSignatures(
    string? Owner,
    string? Student,
    string? Parent,
    string? Witness,
    string WitnessName,
    DateTime? SignedDate)
{
    public static AgreementSignatures None { get; } = new(null, null, null, null, string.Empty, null);

    public string? For(AgreementSigner signer) => signer switch
    {
        AgreementSigner.Owner => Owner,
        AgreementSigner.Student => Student,
        AgreementSigner.Parent => Parent,
        _ => Witness,
    };

    public bool IsSigned => Student is not null || Parent is not null || Owner is not null || Witness is not null;
}

/// <summary>
/// Agreements signed on screen (client decision): the student, the parent or guardian, the hostel owner and a
/// witness sign with the mouse, touchpad or a touch screen. Signatures are kept as PNG files in
/// Documents\Signatures (so backups include them): S12_student.png, S12_parent.png, S12_witness.png and
/// S12_signed.txt (date and witness name) for student 12, and H3_owner.png, the owner's signature saved once for
/// hostel 3 and used on every agreement of that hostel.
/// </summary>
public static class AgreementSignatureService
{
    public static AgreementSignatures Get(int studentId, int hostelId)
    {
        string? Existing(string name) => File.Exists(PathOf(name)) ? PathOf(name) : null;

        string? owner = Existing($"S{studentId}_owner.png") ?? Existing($"H{hostelId}_owner.png");
        (DateTime? signed, string witnessName) = ReadDetails(studentId);
        return new AgreementSignatures(
            owner,
            Existing($"S{studentId}_student.png"),
            Existing($"S{studentId}_parent.png"),
            Existing($"S{studentId}_witness.png"),
            witnessName,
            signed);
    }

    /// <summary>The owner's signature saved for the hostel, or null.</summary>
    public static string? GetHostelOwnerSignature(int hostelId) =>
        File.Exists(PathOf($"H{hostelId}_owner.png")) ? PathOf($"H{hostelId}_owner.png") : null;

    /// <summary>
    /// Saves the signatures given (a null image removes that signature) with the signing date and the witness's
    /// name. With <paramref name="keepOwnerForHostel"/> the owner's signature is also saved for the whole hostel.
    /// </summary>
    public static AgreementSignatures Save(int studentId, int hostelId, IReadOnlyDictionary<AgreementSigner, Image?> signatures,
        string witnessName, DateTime signedDate, bool keepOwnerForHostel)
    {
        witnessName = Validators.Clean(witnessName);
        Validators.CheckLength(witnessName, 100, "Witness name");
        if (signatures.Values.All(image => image is null))
        {
            throw new ValidationException("Please sign at least one box before saving.");
        }

        Directory.CreateDirectory(AppPaths.SignaturesFolder);
        foreach ((AgreementSigner signer, Image? image) in signatures)
        {
            string file = PathOf($"S{studentId}_{signer.ToString().ToLowerInvariant()}.png");
            if (image is null)
            {
                Delete(file);
                continue;
            }
            image.Save(file, ImageFormat.Png);
            if (signer == AgreementSigner.Owner && keepOwnerForHostel)
            {
                image.Save(PathOf($"H{hostelId}_owner.png"), ImageFormat.Png);
            }
        }
        File.WriteAllLines(PathOf($"S{studentId}_signed.txt"),
            [signedDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), witnessName]);
        AppLogger.Info($"Agreement signatures saved for student {studentId}: " +
            string.Join(", ", signatures.Where(s => s.Value is not null).Select(s => s.Key)) + ".");
        return Get(studentId, hostelId);
    }

    /// <summary>Removes all of a student's signatures (for example to sign again, or when the student is deleted).</summary>
    public static void DeleteForStudent(int studentId)
    {
        foreach (string name in new[] { "owner.png", "student.png", "parent.png", "witness.png", "signed.txt" })
        {
            Delete(PathOf($"S{studentId}_{name}"));
        }
    }

    /// <summary>Loads a signature image without keeping the file locked; null when missing or unreadable.</summary>
    public static Image? Load(string? file)
    {
        if (file is null || !File.Exists(file))
        {
            return null;
        }
        try
        {
            using var stream = new MemoryStream(File.ReadAllBytes(file));
            using Image image = Image.FromStream(stream);
            return new Bitmap(image);
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException)
        {
            AppLogger.Error($"The signature {file} could not be read.", ex);
            return null;
        }
    }

    private static (DateTime? Signed, string WitnessName) ReadDetails(int studentId)
    {
        string file = PathOf($"S{studentId}_signed.txt");
        if (!File.Exists(file))
        {
            return (null, string.Empty);
        }
        string[] lines = File.ReadAllLines(file);
        DateTime? signed = lines.Length > 0 &&
            DateTime.TryParseExact(lines[0], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date)
                ? date
                : null;
        return (signed, lines.Length > 1 ? lines[1] : string.Empty);
    }

    private static string PathOf(string name) => Path.Combine(AppPaths.SignaturesFolder, name);

    private static void Delete(string file)
    {
        try
        {
            File.Delete(file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLogger.Error($"Could not delete the signature {file}.", ex);
        }
    }
}
