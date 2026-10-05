namespace HostelManagement.Models;

/// <summary>Values of RoomAllocation.Status.</summary>
public static class AllocationStatus
{
    /// <summary>The student is in this room now; counts towards the room's occupancy.</summary>
    public const string Current = "Current";
    public const string Transferred = "Transferred";
    public const string CheckedOut = "CheckedOut";
}
