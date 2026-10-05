using HostelManagement.Models;

namespace HostelManagement.Forms;

/// <summary>One entry in the left navigation menu.</summary>
/// <param name="Group">Menu section the entry appears under.</param>
/// <param name="Title">Text on the menu button and in the page header.</param>
/// <param name="Description">One line shown under the page header.</param>
/// <param name="CreateView">
/// Creates the screen content for the selected hostel (null when there is none).
/// A new view is created each time the entry is opened or another hostel is selected.
/// </param>
/// <param name="RequiresHostel">True when the screen works on the selected hostel and cannot open without one.</param>
public sealed record NavigationItem(
    string Group,
    string Title,
    string Description,
    Func<Hostel?, UserControl> CreateView,
    bool RequiresHostel = false);
