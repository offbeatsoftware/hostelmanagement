namespace HostelManagement.Forms.Views;

/// <summary>
/// Temporary screen shown for modules that have not been built yet.
/// Each module replaces its placeholder with a real view in its own phase.
/// </summary>
public static class PlaceholderView
{
    public static UserControl Create(string moduleName, string plannedPhase) =>
        new MessageView(moduleName, $"The {moduleName} screen is not available yet. Planned for: {plannedPhase}.");
}
