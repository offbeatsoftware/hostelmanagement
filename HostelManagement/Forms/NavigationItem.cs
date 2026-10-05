namespace HostelManagement.Forms;

/// <summary>One entry in the left navigation menu.</summary>
/// <param name="Group">Menu section the entry appears under.</param>
/// <param name="Title">Text on the menu button and in the page header.</param>
/// <param name="Description">One line shown under the page header.</param>
/// <param name="CreateView">Creates the screen content. A new view is created each time the entry is opened.</param>
public sealed record NavigationItem(
    string Group,
    string Title,
    string Description,
    Func<UserControl> CreateView);
