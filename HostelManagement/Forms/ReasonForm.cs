using HostelManagement.Utilities;

namespace HostelManagement.Forms;

/// <summary>Asks to confirm an action and for its reason (kept in the history). DialogResult.OK gives <see cref="Reason"/>.</summary>
public sealed class ReasonForm : Form
{
    private readonly TextBox _reasonBox;
    private readonly Label _messageLabel;

    public ReasonForm(string title, string question, string actionText, bool danger = true)
    {
        Text = title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Font;
        Font = UiTheme.BodyFont;
        BackColor = Color.White;
        ClientSize = new Size(560, 260);

        var questionLabel = new Label { AutoSize = true, MaximumSize = new Size(520, 0), Margin = new Padding(0, 0, 0, 10), Text = question };
        var reasonLabel = new Label { AutoSize = true, Text = "Reason", Margin = new Padding(0, 4, 0, 2) };
        _reasonBox = new TextBox { Width = 520, MaxLength = 255, PlaceholderText = "For example: entered twice by mistake" };
        _messageLabel = FormFields.CreateMessageLabel();
        _messageLabel.MaximumSize = new Size(300, 0);

        var okButton = new Button { Text = actionText };
        if (danger)
        {
            UiTheme.StyleDangerButton(okButton);
        }
        else
        {
            UiTheme.StylePrimaryButton(okButton);
        }
        okButton.Width = 120;
        okButton.Click += (_, _) =>
        {
            if (_reasonBox.Text.Trim().Length == 0)
            {
                FormFields.ShowError(_messageLabel, "Please enter the reason.");
                return;
            }
            DialogResult = DialogResult.OK;
            Close();
        };
        var cancelButton = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
        UiTheme.StyleSecondaryButton(cancelButton);
        CancelButton = cancelButton;

        FlowLayoutPanel stack = FormFields.CreateStack(questionLabel, reasonLabel, _reasonBox,
            FormFields.CreateButtonRow(okButton, cancelButton, _messageLabel));
        stack.Location = new Point(18, 16);
        Controls.Add(stack);
    }

    public string Reason => _reasonBox.Text.Trim();
}
