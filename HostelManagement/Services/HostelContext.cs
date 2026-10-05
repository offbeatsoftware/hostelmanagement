using HostelManagement.Models;

namespace HostelManagement.Services;

/// <summary>
/// The hostel the admin is currently working on. Colleges, rooms, students and billing
/// screens show only this hostel. Chosen on the sign in screen and in the window header.
/// </summary>
public static class HostelContext
{
    /// <summary>Raised when another hostel is selected, or the selected hostel is renamed or deleted.</summary>
    public static event EventHandler? CurrentHostelChanged;

    public static Hostel? CurrentHostel { get; private set; }

    public static int? CurrentHostelId => CurrentHostel?.HostelId;

    /// <summary>Selects a hostel; null or an unknown id selects the first hostel (or none if there are none).</summary>
    public static void Select(int? hostelId)
    {
        List<Hostel> hostels = HostelService.GetHostels();
        CurrentHostel = hostels.FirstOrDefault(h => h.HostelId == hostelId) ?? hostels.FirstOrDefault();
        CurrentHostelChanged?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>Reloads the selected hostel after hostels were changed (keeps the selection if it still exists).</summary>
    public static void Refresh() => Select(CurrentHostelId);
}
