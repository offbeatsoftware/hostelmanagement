namespace HostelManagement.Models;

/// <summary>One student's unpaid and partly paid invoices on the Pending Dues screen and report.</summary>
public sealed class StudentDue
{
    public int StudentId { get; init; }
    public string StudentName { get; init; } = string.Empty;
    public string StudentStatus { get; init; } = Models.StudentStatus.Active;
    public string RoomNumber { get; init; } = string.Empty;
    public string CollegeName { get; init; } = string.Empty;
    public string Mobile { get; init; } = string.Empty;
    public string ParentName { get; init; } = string.Empty;
    public string ParentMobile { get; init; } = string.Empty;

    /// <summary>When a reminder was last emailed successfully, if ever.</summary>
    public DateTime? LastReminderDate { get; init; }

    /// <summary>The invoices with an amount pending, oldest first.</summary>
    public List<Invoice> Invoices { get; init; } = [];

    /// <summary>The date the dues were calculated for (normally today).</summary>
    public DateTime AsOf { get; init; } = DateTime.Today;

    public int InvoiceCount => Invoices.Count;

    public decimal PendingAmount => Invoices.Sum(i => i.PendingAmount);

    public decimal OverdueAmount => Invoices.Where(i => i.DaysOverdue(AsOf) > 0).Sum(i => i.PendingAmount);

    public DateTime OldestDueDate => Invoices.Min(i => i.DueDate);

    /// <summary>Days the oldest unpaid invoice is past its due date (0 when nothing is overdue yet).</summary>
    public int DaysOverdue => Invoices.Max(i => i.DaysOverdue(AsOf));

    public bool IsOverdue => DaysOverdue > 0;

    /// <summary>"Overdue 20 days", "Due 05 Nov 2026".</summary>
    public string DueText => IsOverdue ? $"Overdue {DaysOverdue} day{(DaysOverdue == 1 ? "" : "s")}" : $"Due {OldestDueDate:dd MMM yyyy}";

    /// <summary>Parent name and mobile on one line.</summary>
    public string ParentText => ParentMobile.Length > 0 ? $"{ParentName}, {ParentMobile}" : ParentName;
}
