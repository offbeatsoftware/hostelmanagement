namespace HostelManagement.Models;

/// <summary>A hostel. Colleges and rooms belong to a hostel.</summary>
public sealed class Hostel
{
    public int HostelId { get; set; }
    public string HostelName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public DateTime? UpdatedDate { get; set; }

    // Read only counts filled in by the hostel list.
    public int CollegeCount { get; set; }
    public int RoomCount { get; set; }
}
