namespace HostelManagement.Models;

/// <summary>One student's fees with an amount pending, on the Fee Reminders screen and the pending fees report.</summary>
public sealed class StudentDue
{
    public int StudentId { get; init; }
    public string StudentName { get; init; } = string.Empty;
    public string StudentStatus { get; init; } = Models.StudentStatus.Active;
    public string RoomNumber { get; init; } = string.Empty;
    public string CollegeName { get; init; } = string.Empty;
    public string Mobile { get; init; } = string.Empty;
    public string FatherName { get; init; } = string.Empty;
    public string FatherMobile { get; init; } = string.Empty;

    /// <summary>Who a fee reminder goes to: the father, or the mother when the father has no email; null when neither has one.</summary>
    public EmailContact? Contact { get; init; }

    /// <summary>When a fee reminder was last emailed successfully, if ever.</summary>
    public DateTime? LastReminderDate { get; init; }

    /// <summary>The invoices (one per academic year) with an amount pending, oldest year first.</summary>
    public List<Invoice> Invoices { get; init; } = [];

    public int InvoiceCount => Invoices.Count;

    public decimal TotalAmount => Invoices.Sum(i => i.TotalAmount);

    public decimal PaidAmount => Invoices.Sum(i => i.PaidAmount);

    public decimal PendingAmount => Invoices.Sum(i => i.PendingAmount);

    /// <summary>"2026-27" or "2025-26, 2026-27".</summary>
    public string YearsText => string.Join(", ", Invoices.Select(i => i.YearText));

    /// <summary>Father name and mobile on one line.</summary>
    public string FatherText => FatherMobile.Length > 0 ? $"{FatherName}, {FatherMobile}" : FatherName;

    /// <summary>"Father: name@gmail.com", or a note that no email is available.</summary>
    public string EmailText => Contact is null ? "No father or mother email" : $"{Contact.Relation}: {Contact.Email}";
}
