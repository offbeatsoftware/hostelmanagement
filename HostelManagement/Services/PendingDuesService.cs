using HostelManagement.Data;
using HostelManagement.Models;

namespace HostelManagement.Services;

/// <summary>
/// Pending fees (client decisions, version 1.2): every invoice with an amount pending, grouped per student.
/// There are no due dates: the admin sends a fee reminder whenever he wants. Students who have left are
/// included while they still owe money.
/// </summary>
public static class PendingDuesService
{
    /// <summary>Students of the hostel with an amount pending, highest pending amount first.</summary>
    public static List<StudentDue> GetDues(int hostelId)
    {
        Dictionary<int, Student> students = StudentService.GetStudents(hostelId).ToDictionary(s => s.StudentId);
        Dictionary<int, DateTime> lastReminders = EmailHistoryRepository.GetLastReminderDates(hostelId);

        return InvoiceService.GetInvoices(hostelId)
            .Where(i => i.PendingAmount > 0)
            .GroupBy(i => i.StudentId)
            .Select(group =>
            {
                Student? student = students.GetValueOrDefault(group.Key);
                return new StudentDue
                {
                    StudentId = group.Key,
                    StudentName = student?.StudentName ?? group.First().StudentName,
                    StudentStatus = student?.Status ?? StudentStatus.Active,
                    RoomNumber = student?.RoomNumber ?? string.Empty,
                    CollegeName = student?.CollegeName ?? string.Empty,
                    Mobile = student?.Mobile ?? string.Empty,
                    FatherName = student?.FatherName ?? string.Empty,
                    FatherMobile = student?.FatherMobile ?? string.Empty,
                    Contact = student?.FeeContact,
                    Invoices = group.OrderBy(i => i.AcademicYear).ThenBy(i => i.InvoiceId).ToList(),
                    LastReminderDate = lastReminders.TryGetValue(group.Key, out DateTime last) ? last : null,
                };
            })
            .OrderByDescending(d => d.PendingAmount)
            .ThenBy(d => d.StudentName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>One student's pending fees, or null when nothing is pending.</summary>
    public static StudentDue? GetDue(int hostelId, int studentId) =>
        GetDues(hostelId).FirstOrDefault(d => d.StudentId == studentId);
}
