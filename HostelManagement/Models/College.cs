namespace HostelManagement.Models;

/// <summary>A college that hostel students attend.</summary>
public sealed class College
{
    public int CollegeId { get; set; }
    public string CollegeName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
}
