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
            AutoSize = true,
            MaximumSize = new Size(820, 0),
            Margin = new Padding(0, 0, 0, 6),
            Font = UiTheme.BodyFont,
            ForeColor = UiTheme.TextMuted,
            Text = message,
        };
        TableLayoutPanel card = FormFields.CreateAutoCard(heading, text);
        Controls.Add(card);
    }
}
