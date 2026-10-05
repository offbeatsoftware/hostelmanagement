namespace HostelManagement.Models;

/// <summary>A college whose students stay in a hostel. Each college belongs to one hostel.</summary>
public sealed class College
{
    public int CollegeId { get; set; }
    public int HostelId { get; set; }
    public string CollegeName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
}
