namespace HostelManagement.Models;

/// <summary>
/// Who a room is for. Stored as the student gender it accepts (Male / Female) and shown as
/// Boys / Girls; a student can only be allocated to a room of their own gender (client decision).
/// </summary>
public static class RoomGender
{
    public const string Male = "Male";
    public const string Female = "Female";

    public static IReadOnlyList<string> All { get; } = [Male, Female];

    public static string DisplayName(string gender) => gender switch
    {
        Male => "Boys",
        Female => "Girls",
        _ => string.Empty,
    };
}

/// <summary>A room. Capacity and rent come from its sharing type; occupancy from current allocations.</summary>
public sealed class Room
{
    public int RoomId { get; set; }
    public int HostelId { get; set; }
    public string RoomNumber { get; set; } = string.Empty;
    public string Floor { get; set; } = string.Empty;
    public int SharingTypeId { get; set; }

    /// <summary><see cref="RoomGender.Male"/> or <see cref="RoomGender.Female"/>.</summary>
    public string Gender { get; set; } = string.Empty;

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

    /// <summary>Boys or Girls.</summary>
    public string RoomFor => RoomGender.DisplayName(Gender);

    /// <summary>For drop-downs, for example "101 (Double, 1 free bed)".</summary>
    public string DisplayName => $"{RoomNumber} ({SharingName}, {Available} free {(Available == 1 ? "bed" : "beds")})";
}
