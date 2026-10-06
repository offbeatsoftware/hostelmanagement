using HostelManagement.Data;
using HostelManagement.Models;

namespace HostelManagement.Services;

/// <summary>
/// Pending dues (client decisions, Phase 10): every unpaid or partly paid invoice, grouped per student;
/// an invoice is due 15 days after its date and overdue after that (no late fee); students who have
/// left are included while they still owe money.
/// </summary>
public static class PendingDuesService
{
    /// <summary>Students of the hostel with an amount pending, most overdue first.</summary>
    public static List<StudentDue> GetDues(int hostelId, DateTime asOf)
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
                    ParentName = student?.ParentName ?? string.Empty,
                    ParentMobile = student?.ParentMobile ?? string.Empty,
                    Invoices = group.OrderBy(i => i.InvoiceDate).ThenBy(i => i.InvoiceId).ToList(),
                    AsOf = asOf.Date,
                    LastReminderDate = lastReminders.TryGetValue(group.Key, out DateTime last) ? last : null,
                };
            })
            .OrderByDescending(d => d.DaysOverdue)
            .ThenByDescending(d => d.PendingAmount)
            .ThenBy(d => d.StudentName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }
}
