namespace HostelManagement.Models;

/// <summary>
/// A hostel service such as Wi-Fi, laundry or transport. Services included in the rent cost nothing
/// extra; other services are charged per month to the students who use them.
/// </summary>
public sealed class ServiceItem
{
    public int ServiceId { get; set; }
    public int HostelId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public bool IsIncludedInRent { get; set; }

    /// <summary>Monthly charge per student; 0 for services included in the rent.</summary>
    public decimal MonthlyRate { get; set; }

    public bool IsActive { get; set; } = true;

    // Read only, filled in when services are listed.
    public int StudentCount { get; set; }

    public string ChargeText => IsIncludedInRent ? "Included in rent" : "Extra, per month";
    public string Status => IsActive ? "Active" : "Inactive";
}

/// <summary>A student using an extra service from a start date until an end date (empty while in use).</summary>
public sealed class StudentServiceUse
{
    public int StudentServiceId { get; set; }
    public int StudentId { get; set; }
    public int ServiceId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    public string ServiceName { get; set; } = string.Empty;
    public decimal MonthlyRate { get; set; }
}
