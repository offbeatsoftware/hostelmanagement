namespace HostelManagement.Models;

/// <summary>
/// A student's stay in a room. Check-in creates a Current allocation; transfer and check-out
/// close it (CheckOutDate and Status) so the history is kept.
/// </summary>
public sealed class RoomAllocation
{
    public int AllocationId { get; set; }
    public int StudentId { get; set; }
    public int RoomId { get; set; }
    public DateTime CheckInDate { get; set; }
    public DateTime? CheckOutDate { get; set; }
    public string Status { get; set; } = AllocationStatus.Current;
    public string Remarks { get; set; } = string.Empty;

    /// <summary>The bed in the room: 1 up to the room's capacity (empty only for very old records).</summary>
    public int? BedNumber { get; set; }

    // Read only values filled in when allocations are listed.
    public string StudentName { get; set; } = string.Empty;
    public string StudentMobile { get; set; } = string.Empty;
    public string RoomNumber { get; set; } = string.Empty;

    public string BedText => BedNumber is int bed ? $"Bed {bed}" : string.Empty;

    /// <summary>Room and bed, for example "101, bed 2".</summary>
    public string RoomAndBed => BedNumber is int bed ? $"{RoomNumber}, bed {bed}" : RoomNumber;

    /// <summary>Nights in the room so far (current) or in total (closed).</summary>
    public int Days => ((CheckOutDate ?? DateTime.Today) - CheckInDate).Days;

    public string StatusText => Status switch
    {
        AllocationStatus.Current => "In room",
        AllocationStatus.Transferred => "Transferred",
        AllocationStatus.CheckedOut => "Checked out",
        _ => Status,
    };
}
