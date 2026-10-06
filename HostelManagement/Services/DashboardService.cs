using HostelManagement.Models;

namespace HostelManagement.Services;

/// <summary>
/// Dashboard of the selected hostel (client decisions, Phase 12): students, beds, amount invoiced this
/// academic year, amount received this month, pending and overdue totals, the five most overdue students,
/// the latest payments and check-ins, and payments received per month of the academic year.
/// </summary>
public static class DashboardService
{
    public const int ListSize = 5;

    public static DashboardData Get(int hostelId, DateTime asOf)
    {
        DateTime today = asOf.Date;
        DateTime yearStart = BillingPeriods.ForAcademicYear(today, BillingFrequency.HalfYearly)[0].From;
        DateTime yearEnd = yearStart.AddYears(1);
        var monthStart = new DateTime(today.Year, today.Month, 1);

        List<Student> students = StudentService.GetStudents(hostelId);
        List<Room> rooms = RoomService.GetRooms(hostelId);
        List<Invoice> invoices = InvoiceService.GetInvoices(hostelId);
        List<Payment> payments = PaymentService.GetPayments(hostelId);
        List<StudentDue> dues = PendingDuesService.GetDues(hostelId, today);

        return new DashboardData
        {
            AsOf = today,
            AcademicYear = InvoiceService.AcademicYearLabel(today),
            ActiveStudents = students.Count(s => s.Status == StudentStatus.Active),
            LeftStudents = students.Count(s => s.Status == StudentStatus.Left),
            TotalBeds = rooms.Where(r => r.IsActive).Sum(r => r.Capacity),
            OccupiedBeds = rooms.Sum(r => r.Occupied),
            FreeBeds = rooms.Sum(r => r.Available),
            InvoicedThisYear = invoices.Where(i => i.BillingFrom >= yearStart && i.BillingFrom < yearEnd).Sum(i => i.TotalAmount),
            ReceivedThisMonth = payments.Where(p => p.PaymentDate >= monthStart && p.PaymentDate <= today).Sum(p => p.Amount),
            PendingAmount = dues.Sum(d => d.PendingAmount),
            OverdueAmount = dues.Sum(d => d.OverdueAmount),
            OverdueStudents = dues.Count(d => d.IsOverdue),
            MostOverdue = dues.Where(d => d.IsOverdue).Take(ListSize).ToList(),
            LatestPayments = payments.Take(ListSize).ToList(),
            RecentCheckIns = AllocationService.GetAllocations(hostelId, includeHistory: true)
                .OrderByDescending(a => a.CheckInDate)
                .ThenByDescending(a => a.AllocationId)
                .Take(ListSize)
                .ToList(),
            PaymentsPerMonth = Enumerable.Range(0, 12)
                .Select(i => yearStart.AddMonths(i))
                .Select(month => new MonthTotal(month,
                    payments.Where(p => p.PaymentDate >= month && p.PaymentDate < month.AddMonths(1)).Sum(p => p.Amount)))
                .ToList(),
        };
    }
}
