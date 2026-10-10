using HostelManagement.Data;
using HostelManagement.Models;
using HostelManagement.Utilities;

namespace HostelManagement.Services;

/// <summary>
/// Night attendance (client decisions): marked once a day by the admin for the students in a room that night;
/// everyone starts as present and the admin marks who is absent. Earlier dates can be opened and corrected,
/// future dates cannot be marked. The mother (or the father, when the mother has no email) of each absent student is emailed when the admin
/// clicks the button.
/// </summary>
public static class AttendanceService
{
    /// <summary>The students in a room of the hostel on the night of the date, with what was saved for them.</summary>
    public static AttendanceSheet GetSheet(int hostelId, DateTime date)
    {
        date = date.Date;
        Dictionary<int, Student> students = StudentService.GetStudents(hostelId).ToDictionary(s => s.StudentId);
        Dictionary<int, AttendanceRepository.Saved> saved = AttendanceRepository.GetForDate(hostelId, date)
            .ToDictionary(a => a.StudentId);

        // In the room that night: checked in on or before the date and not checked out or transferred that day.
        List<RoomAllocation> nightAllocations = AllocationService.GetAllocations(hostelId, includeHistory: true)
            .Where(a => a.CheckInDate.Date <= date && (a.CheckOutDate is null || a.CheckOutDate.Value.Date > date))
            .GroupBy(a => a.StudentId)
            .Select(g => g.OrderByDescending(a => a.CheckInDate).First())
            .ToList();

        var entries = nightAllocations
            .Where(a => students.ContainsKey(a.StudentId))
            .Select(a =>
            {
                Student student = students[a.StudentId];
                // Attendance emails go to the mother, or to the father when the mother has no email (client decision).
                EmailContact? contact = student.AttendanceContact;
                AttendanceRepository.Saved? record = saved.GetValueOrDefault(a.StudentId);
                return new AttendanceEntry
                {
                    AttendanceId = record?.AttendanceId,
                    StudentId = a.StudentId,
                    StudentName = student.StudentName,
                    RoomNumber = a.RoomNumber,
                    BedNumber = a.BedNumber,
                    ParentName = contact?.Name ?? string.Empty,
                    ParentEmail = contact?.Email ?? string.Empty,
                    ParentMobile = contact?.Relation == "Father" || student.MotherMobile.Length == 0 ? student.FatherMobile : student.MotherMobile,
                    IsPresent = record?.IsPresent ?? true,
                    Remarks = record?.Remarks ?? string.Empty,
                    ParentEmailedDate = record?.ParentEmailedDate,
                };
            })
            .OrderBy(e => e.RoomNumber, NaturalComparer.Instance)
            .ThenBy(e => e.BedNumber ?? int.MaxValue)
            .ThenBy(e => e.StudentName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return new AttendanceSheet { Date = date, Entries = entries };
    }

    /// <summary>Saves the attendance of every student on the sheet for the date.</summary>
    public static AttendanceSheet Save(int hostelId, DateTime date, IReadOnlyCollection<AttendanceEntry> entries)
    {
        date = date.Date;
        if (date > DateTime.Today)
        {
            throw new ValidationException("Attendance cannot be marked for a future date.");
        }

        HashSet<int> onSheet = GetSheet(hostelId, date).Entries.Select(e => e.StudentId).ToHashSet();
        foreach (AttendanceEntry entry in entries)
        {
            if (!onSheet.Contains(entry.StudentId))
            {
                throw new ValidationException($"{entry.StudentName} was not in a room of this hostel on {date:dd MMM yyyy}.");
            }
            entry.Remarks = Validators.Clean(entry.Remarks);
            Validators.CheckLength(entry.Remarks, 255, $"The remarks for {entry.StudentName}");
        }

        DateTime now = DateTime.Now;
        Db.InTransaction((connection, transaction) =>
        {
            foreach (AttendanceEntry entry in entries)
            {
                AttendanceRepository.Upsert(connection, transaction, entry.StudentId, date, entry.IsPresent, entry.Remarks, now);
            }
        });

        AppLogger.Info($"Attendance saved for hostel {hostelId} on {date:yyyy-MM-dd}: {entries.Count(e => !e.IsPresent)} absent.");
        return GetSheet(hostelId, date);
    }
}
