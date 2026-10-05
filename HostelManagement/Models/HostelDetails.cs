namespace HostelManagement.Models;

/// <summary>The hostel's own details. The database holds a single row.</summary>
public sealed class HostelDetails
{
    public int HostelId { get; set; }
    public string HostelName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public DateTime? UpdatedDate { get; set; }
}
