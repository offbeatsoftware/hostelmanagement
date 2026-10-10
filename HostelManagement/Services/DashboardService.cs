using HostelManagement.Models;

namespace HostelManagement.Services;

/// <summary>
/// Dashboard of the selected hostel: students, beds, the fees of this academic year, the amount received this
/// month and year, the pending fees, the five students with the highest pending fees, the latest payments and
/// check-ins, and payments received per month of the academic year.
/// </summary>
public static class DashboardService
{
    public const int ListSize = 5;

    public static DashboardData Get(int hostelId, DateTime asOf)
    {
        DateTime today = asOf.Date;
        int academicYear = Models.AcademicYear.Of(today);
        DateTime yearStart = Models.AcademicYear.Start(academicYear);
        var monthStart = new DateTime(today.Year, today.Month, 1);

        List<Student> students = StudentService.GetStudents(hostelId);
        List<Room> rooms = RoomService.GetRooms(hostelId);
        List<Invoice> invoices = InvoiceService.GetInvoices(hostelId);
        List<Payment> payments = PaymentService.GetPayments(hostelId);
        List<StudentDue> dues = PendingDuesService.GetDues(hostelId);

        return new DashboardData
        {
            AsOf = today,
            AcademicYear = Models.AcademicYear.Label(academicYear),
            ActiveStudents = students.Count(s => s.Status == StudentStatus.Active),
            LeftStudents = students.Count(s => s.Status == StudentStatus.Left),
            TotalBeds = rooms.Where(r => r.IsActive).Sum(r => r.Capacity),
            OccupiedBeds = rooms.Sum(r => r.Occupied),
            FreeBeds = rooms.Sum(r => r.Available),
            InvoicedThisYear = invoices.Where(i => i.AcademicYear == academicYear).Sum(i => i.TotalAmount),
            ReceivedThisMonth = payments.Where(p => p.PaymentDate >= monthStart && p.PaymentDate <= today).Sum(p => p.Amount),
            ReceivedThisYear = payments.Where(p => p.PaymentDate >= yearStart && p.PaymentDate <= today).Sum(p => p.Amount),
            PendingAmount = dues.Sum(d => d.PendingAmount),
            PendingStudents = dues.Count,
            HighestPending = dues.Take(ListSize).ToList(),
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
