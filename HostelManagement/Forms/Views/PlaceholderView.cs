using HostelManagement.Utilities;

namespace HostelManagement.Forms.Views;

/// <summary>
/// Temporary screen shown for modules that have not been built yet.
/// Each module replaces its placeholder with a real view in its own phase.
/// </summary>
public sealed class PlaceholderView : UserControl
{
    public PlaceholderView(string moduleName, string plannedPhase)
    {
        Dock = DockStyle.Fill;
        BackColor = UiTheme.ContentBackground;

        var card = new Panel
        {
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Dock = DockStyle.Top,
            Height = 120,
            Padding = new Padding(20),
        };

        var message = new Label
        {
            Dock = DockStyle.Fill,
            Font = UiTheme.BodyFont,
            ForeColor = UiTheme.TextMuted,
            Text = $"The {moduleName} screen is not available yet.\n\nPlanned for: {plannedPhase}.",
        };

        var heading = new Label
        {
            Dock = DockStyle.Top,
            Height = 30,
            Font = UiTheme.BodyBoldFont,
            ForeColor = UiTheme.TextPrimary,
            Text = moduleName,
        };

        card.Controls.Add(message);
        card.Controls.Add(heading);
        Controls.Add(card);
    }
}
