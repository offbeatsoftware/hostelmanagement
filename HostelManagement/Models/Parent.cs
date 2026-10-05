namespace HostelManagement.Models;

/// <summary>A parent or guardian of a student. The primary contact receives invoices and reminders.</summary>
public sealed class Parent
{
    public int ParentId { get; set; }
    public int StudentId { get; set; }
    public string ParentName { get; set; } = string.Empty;
    public string Relationship { get; set; } = string.Empty;
    public string Mobile { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public bool IsPrimaryContact { get; set; }

    // Read only, filled in when parents are listed.
    public string StudentName { get; set; } = string.Empty;

    public string PrimaryText => IsPrimaryContact ? "Yes" : string.Empty;
}
