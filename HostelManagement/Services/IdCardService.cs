using HostelManagement.Data;
using HostelManagement.Models;

namespace HostelManagement.Services;

/// <summary>Everything printed on one I-card.</summary>
public sealed record IdCard(
    string HostelName,
    string HostelPhone,
    string StudentName,
    string FatherName,
    string CollegeName,
    string RoomText,
    string StudentMobile,
    string FatherMobile,
    string? PhotoFile,
    DateTime ValidTill,
    string AcademicYear);

/// <summary>
/// Student and transport I-cards (client decisions, version 1.2), with the student's photo. Any active student can
/// get a student I-card; a transport I-card only for a student whose fee for the academic year includes transport.
/// The admin types the "Valid till" date each time cards are printed (client decision); it starts as the last day
/// of the academic year.
/// </summary>
public static class IdCardService
{
    /// <summary>The "Valid till" date offered when printing: the last day of the academic year.</summary>
    public static DateTime DefaultValidTill(DateTime today) => AcademicYear.End(AcademicYear.Of(today));

    /// <summary>
    /// The cards for the chosen students, valid till the given date (the end of the academic year when null);
    /// students who cannot get one are returned as problems.
    /// </summary>
    public static (List<IdCard> Cards, List<string> Problems) Prepare(IReadOnlyList<int> studentIds, bool transport, DateTime today,
        DateTime? validTill = null)
    {
        if (studentIds.Count == 0)
        {
            throw new ValidationException("Please select one or more students.");
        }
        DateTime validUntil = (validTill ?? DefaultValidTill(today)).Date;
        if (validUntil < today.Date)
        {
            throw new ValidationException("The Valid till date cannot be in the past.");
        }

        int academicYear = AcademicYear.Of(today);
        var cards = new List<IdCard>();
        var problems = new List<string>();
        foreach (int studentId in studentIds)
        {
            Student? student = StudentService.GetStudent(studentId);
            if (student is null)
            {
                continue;
            }
            if (student.Status != StudentStatus.Active)
            {
                problems.Add($"{student.StudentName}: has left the hostel.");
                continue;
            }
            if (transport && InvoiceRepository.GetForYear(studentId, academicYear) is not { HasTransport: true })
            {
                problems.Add($"{student.StudentName}: no transport in the {AcademicYear.Label(academicYear)} fee.");
                continue;
            }

            College? college = student.CollegeId is int collegeId ? CollegeRepository.Get(collegeId) : null;
            Hostel? hostel = HostelService.GetHostel(student.HostelId);
            RoomAllocation? room = AllocationRepository.GetCurrentForStudent(studentId);
            string? photo = StudentFileService.Exists(student.PhotoPath) ? StudentFileService.FullPath(student.PhotoPath) : null;
            if (photo is null)
            {
                problems.Add($"{student.StudentName}: no photo (the card has an empty photo box). Add one on the Students screen.");
            }

            cards.Add(new IdCard(
                hostel?.HostelName ?? string.Empty,
                hostel?.Phone ?? string.Empty,
                student.StudentName,
                student.FatherName,
                student.CollegeName,
                room is null ? string.Empty : $"Room {room.RoomAndBed}",
                student.Mobile,
                student.FatherMobile,
                photo,
                validUntil,
                AcademicYear.Label(academicYear)));
        }
        return (cards, problems);
    }

    /// <summary>A file name such as ICards_2026-10-12.pdf or TransportCards_2026-10-12.pdf.</summary>
    public static string FileName(bool transport, DateTime today) =>
        $"{(transport ? "TransportCards" : "ICards")}_{today:yyyy-MM-dd}.pdf";
}
