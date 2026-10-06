namespace HostelManagement.Models;

/// <summary>Payments received in one month of the academic year (for the dashboard chart).</summary>
public sealed record MonthTotal(DateTime Month, decimal Amount)
{
    public string Label => Month.ToString("MMM", System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>The figures, short lists and chart shown on the dashboard for one hostel.</summary>
public sealed class DashboardData
{
    public DateTime AsOf { get; init; }
    public string AcademicYear { get; init; } = string.Empty;

    public int ActiveStudents { get; init; }
    public int LeftStudents { get; init; }
    public int TotalStudents => ActiveStudents + LeftStudents;

    public int TotalBeds { get; init; }
    public int OccupiedBeds { get; init; }
    public int FreeBeds { get; init; }

    public decimal InvoicedThisYear { get; init; }
    public decimal ReceivedThisMonth { get; init; }
    public decimal PendingAmount { get; init; }
    public decimal OverdueAmount { get; init; }
    public int OverdueStudents { get; init; }

    public List<StudentDue> MostOverdue { get; init; } = [];
    public List<Payment> LatestPayments { get; init; } = [];
    public List<RoomAllocation> RecentCheckIns { get; init; } = [];

    /// <summary>July to June of the current academic year.</summary>
    public List<MonthTotal> PaymentsPerMonth { get; init; } = [];
}
