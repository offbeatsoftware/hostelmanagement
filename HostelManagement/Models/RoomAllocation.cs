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

    // Read only values filled in when allocations are listed.
    public string StudentName { get; set; } = string.Empty;
    public string StudentMobile { get; set; } = string.Empty;
    public string RoomNumber { get; set; } = string.Empty;

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
