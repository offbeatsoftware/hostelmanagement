using System.Globalization;
using System.Text.RegularExpressions;
using HostelManagement.Data;
using HostelManagement.Models;
using HostelManagement.Reports;
using HostelManagement.Utilities;

namespace HostelManagement.Services;

/// <summary>The agreement text with the student's details filled in, ready to be written as a PDF.</summary>
public sealed record AgreementDocument(string StudentName, string ParentName, string FilledText);

/// <summary>
/// Residency agreement (client decision): the client's agreement text with the student's name, father,
/// address, room, bed and annual fee filled in. The text is editable (stored in AppSetting); a new installation
/// starts with the client's sample. Annual fee = the room rent plus the transport agreed with the student for the
/// academic year (the student's invoice for that year).
/// </summary>
public static partial class AgreementService
{
    private const string TemplateSetting = "Agreement.Text";

    /// <summary>Marks a filled in value so that the PDF prints it in bold.</summary>
    public const char ValueStart = '\u0001';
    public const char ValueEnd = '\u0002';

    /// <summary>The {Fields} that can be used in the agreement text.</summary>
    public static IReadOnlyList<string> Fields { get; } =
    [
        "AgreementDate", "StudentName", "RelationPrefix", "ParentName", "Address", "StudentMobile", "ParentMobile",
        "CollegeName", "HostelName", "HostelType", "RoomNumber", "BedNumber", "BedsInRoom", "CheckInDate",
        "AnnualFee", "AnnualRent", "TransportFee",
    ];

    public static string DefaultTemplate => Normalise(AgreementDefaults.Text);

    public static string GetTemplate() =>
        AppSettingRepository.GetAll().TryGetValue(TemplateSetting, out string? text) && text.Trim().Length > 0
            ? Normalise(text)
            : DefaultTemplate;

    public static void SaveTemplate(string text)
    {
        text = Normalise(text);
        if (text.Trim().Length == 0)
        {
            throw new ValidationException("Please enter the agreement text (or click Restore Default).");
        }
        List<string> unknown = FieldPattern().Matches(text).Select(m => m.Groups[1].Value)
            .Where(f => !Fields.Contains(f)).Distinct().ToList();
        if (unknown.Count > 0)
        {
            throw new ValidationException(
                $"The agreement uses {string.Join(", ", unknown.Select(f => "{" + f + "}"))}, which is not available. " +
                $"Available: {string.Join(", ", Fields.Select(f => "{" + f + "}"))}.");
        }
        AppSettingRepository.SaveAll(new Dictionary<string, string> { [TemplateSetting] = text });
        AppLogger.Info("Agreement text saved.");
    }

    /// <summary>The agreement for the student's current room and bed (or the last room, for a student who has left).</summary>
    public static AgreementDocument Prepare(int studentId, DateTime agreementDate)
    {
        Student student = StudentService.GetStudent(studentId)
            ?? throw new ValidationException("Please select the student.");
        RoomAllocation allocation = AllocationRepository.GetCurrentForStudent(studentId)
            ?? AllocationRepository.GetForStudent(studentId).LastOrDefault()
            ?? throw new ValidationException($"{student.StudentName} has not been checked in to a room yet. Check in the student first.");
        Room room = RoomRepository.Get(allocation.RoomId)
            ?? throw new ValidationException("The student's room no longer exists.");
        Hostel? hostel = HostelService.GetHostel(room.HostelId);
        College? college = CollegeRepository.Get(student.CollegeId);

        // The fee of the academic year of the agreement, or the student's latest fee.
        Invoice? fee = InvoiceRepository.GetForYear(studentId, AcademicYear.Of(agreementDate))
            ?? InvoiceRepository.GetForStudent(studentId).FirstOrDefault();

        var values = new Dictionary<string, string>
        {
            ["AgreementDate"] = Date(agreementDate),
            ["StudentName"] = student.StudentName,
            ["RelationPrefix"] = student.Gender switch
            {
                RoomGender.Male => "S/o",
                RoomGender.Female => "D/o",
                _ => "D/o, S/o",
            },
            ["ParentName"] = student.FatherName,
            ["Address"] = student.Address.ReplaceLineEndings(", "),
            ["StudentMobile"] = student.Mobile,
            ["ParentMobile"] = student.FatherMobile,
            ["CollegeName"] = college?.CollegeName ?? string.Empty,
            ["HostelName"] = hostel?.HostelName ?? string.Empty,
            ["HostelType"] = room.Gender == RoomGender.Female ? "Girls" : "Boys",
            ["RoomNumber"] = room.RoomNumber,
            ["BedNumber"] = allocation.BedNumber?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            ["BedsInRoom"] = room.Capacity switch { 1 => "one", 2 => "two", 3 => "three", 4 => "four", _ => room.Capacity.ToString(CultureInfo.InvariantCulture) },
            ["CheckInDate"] = Date(allocation.CheckInDate),
            ["AnnualFee"] = fee is null ? string.Empty : $"{Whole(fee.TotalAmount)}/- ({PdfText.AmountInWords(fee.TotalAmount)})",
            ["AnnualRent"] = fee is null ? string.Empty : $"{Whole(fee.RoomRent)}/-",
            ["TransportFee"] = fee is null ? string.Empty : $"{Whole(fee.TransportAmount)}/-",
        };

        string filled = FieldPattern().Replace(GetTemplate(), m =>
            values.TryGetValue(m.Groups[1].Value, out string? value)
                // A missing value leaves a dotted line to fill in by hand, as on the printed form.
                ? $"{ValueStart}{(value.Trim().Length > 0 ? value.Trim() : "................................")}{ValueEnd}"
                : m.Value);
        return new AgreementDocument(student.StudentName, student.FatherName, filled);
    }

    /// <summary>A file name such as Agreement_Aman-Sharma_2026-10-06.pdf.</summary>
    public static string FileName(AgreementDocument document, DateTime date) =>
        $"Agreement_{ReportService.SafeName(document.StudentName)}_{date:yyyy-MM-dd}.pdf";

    private static string Normalise(string text) =>
        string.Join(Environment.NewLine, text.ReplaceLineEndings("\n").Split('\n').Select(l => l.TrimEnd())).Trim();

    private static string Whole(decimal amount) => amount.ToString("N0", Money.Culture);

    private static string Date(DateTime date) => date.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);

    [GeneratedRegex(@"\{([A-Za-z]+)\}")]
    private static partial Regex FieldPattern();
}
