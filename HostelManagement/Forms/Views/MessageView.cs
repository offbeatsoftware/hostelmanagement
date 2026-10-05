using HostelManagement.Utilities;

namespace HostelManagement.Forms.Views;

/// <summary>A card with a heading and a short message, for example when a hostel must be added first.</summary>
public sealed class MessageView : UserControl
{
    public MessageView(string heading, string message)
    {
        Dock = DockStyle.Fill;
        BackColor = UiTheme.ContentBackground;

        var text = new Label
        {
            Dock = DockStyle.Fill,
            Font = UiTheme.BodyFont,
            ForeColor = UiTheme.TextMuted,
            Text = message,
        };
        Panel card = FormFields.CreateCard(heading, text);
        card.Dock = DockStyle.Top;
        card.Height = 110;
        Controls.Add(card);
    }
}
