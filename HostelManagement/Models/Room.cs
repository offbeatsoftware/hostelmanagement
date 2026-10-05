namespace HostelManagement.Models;

/// <summary>A room. Capacity and rent come from its sharing type; occupancy from current allocations.</summary>
public sealed class Room
{
    public int RoomId { get; set; }
    public string RoomNumber { get; set; } = string.Empty;
    public string Floor { get; set; } = string.Empty;
    public int SharingTypeId { get; set; }
    public bool IsActive { get; set; } = true;
    public string Remarks { get; set; } = string.Empty;

    // Read only values filled in when rooms are loaded.
    public string SharingName { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public decimal Rent { get; set; }

    /// <summary>Students currently allocated to the room.</summary>
    public int Occupied { get; set; }

    /// <summary>Free beds. An inactive room has no free beds.</summary>
    public int Available => IsActive ? Math.Max(Capacity - Occupied, 0) : 0;

    public string Status => IsActive ? "Active" : "Inactive";
}
