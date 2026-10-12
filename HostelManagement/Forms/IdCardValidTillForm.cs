using HostelManagement.Utilities;

namespace HostelManagement.Forms;

/// <summary>Asks for the "Valid till" date printed on the I-cards (client decision); it starts as the end of the academic year.</summary>
public sealed class IdCardValidTillForm : Form
{
    private readonly DateTimePicker _datePicker;

    public IdCardValidTillForm(bool transport, int cardCount, DateTime suggested)
    {
        Text = transport ? "Transport I-Cards" : "Student I-Cards";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Font;
        Font = UiTheme.BodyFont;
        BackColor = Color.White;
        ClientSize = new Size(420, 170);

        var intro = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(380, 0),
            Location = new Point(20, 18),
            Text = $"I-cards for {cardCount} student(s). Enter the date printed after \"Valid till\" on the cards.",
        };
        var dateLabel = new Label { AutoSize = true, Location = new Point(20, 72), Text = "Valid till" };
        _datePicker = new DateTimePicker
        {
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "dd MMM yyyy",
            Location = new Point(110, 68),
            Width = 160,
            MinDate = DateTime.Today,
            Value = suggested < DateTime.Today ? DateTime.Today : suggested,
        };

        var okButton = new Button { Text = "Make Cards", Location = new Point(170, 120), DialogResult = DialogResult.OK };
        UiTheme.StylePrimaryButton(okButton);
        okButton.Width = 120;
        var cancelButton = new Button { Text = "Cancel", Location = new Point(300, 120), DialogResult = DialogResult.Cancel };
        UiTheme.StyleSecondaryButton(cancelButton);

        AcceptButton = okButton;
        CancelButton = cancelButton;
        Controls.AddRange([intro, dateLabel, _datePicker, okButton, cancelButton]);
    }

    /// <summary>The chosen date, read after DialogResult.OK.</summary>
    public DateTime ValidTill => _datePicker.Value.Date;
}
