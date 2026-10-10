namespace HostelManagement.Models;

/// <summary>Single, Double or Triple sharing. It only decides how many students fit in a room.</summary>
public sealed class SharingType
{
    public int SharingTypeId { get; set; }
    public int HostelId { get; set; }
    public string SharingName { get; set; } = string.Empty;
    public int Capacity { get; set; }

    /// <summary>For drop-downs, for example "Double (2 beds)".</summary>
    public string DisplayName => $"{SharingName} ({Capacity} {(Capacity == 1 ? "bed" : "beds")})";
}
