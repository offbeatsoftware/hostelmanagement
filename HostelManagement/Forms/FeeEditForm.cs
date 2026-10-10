using HostelManagement.Models;
using HostelManagement.Services;
using HostelManagement.Utilities;

namespace HostelManagement.Forms;

/// <summary>
/// Changes the room rent and transport agreed with a student for one academic year. The new total cannot be
/// less than what has already been paid. DialogResult.OK means it was saved.
/// </summary>
public sealed class FeeEditForm : Form
{
    private readonly Invoice _invoice;
    private readonly TextBox _rentBox;
    private readonly TextBox _transportBox;
    private readonly TextBox _remarksBox;
    private readonly Label _totalLabel;
    private readonly Label _messageLabel;

    public FeeEditForm(Invoice invoice)
    {
        _invoice = invoice;

        Text = $"Edit Fee: {invoice.StudentName}, {invoice.YearText}";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Font;
        Font = UiTheme.BodyFont;
        BackColor = Color.White;
        ClientSize = new Size(560, 400);

        TableLayoutPanel fields = FormFields.CreateTable(labelWidth: 150, inputWidth: 370);
        FormFields.AddRow(fields, "Invoice", InfoLabel($"{invoice.InvoiceNumber} ({invoice.YearText})"));
        FormFields.AddRow(fields, "Student", InfoLabel(invoice.StudentName));
        FormFields.AddRow(fields, "Paid so far", InfoLabel(Money.Format(invoice.PaidAmount)));
        _rentBox = MoneyBox(fields, "Room rent / year", required: true);
        _transportBox = MoneyBox(fields, "Transport / year", required: false);
        _transportBox.PlaceholderText = "0 if no transport";
        _totalLabel = InfoLabel(string.Empty);
        FormFields.AddRow(fields, "Total fee", _totalLabel);
        _remarksBox = FormFields.AddTextBox(fields, "Remarks", 255, multiline: true);
        fields.Location = new Point(20, 20);

        _rentBox.Text = invoice.RoomRent.ToString("N2", Money.Culture);
        _transportBox.Text = invoice.TransportAmount.ToString("N2", Money.Culture);
        _remarksBox.Text = invoice.Remarks;
        _rentBox.TextChanged += (_, _) => ShowTotal();
        _transportBox.TextChanged += (_, _) => ShowTotal();
        ShowTotal();

        _messageLabel = FormFields.CreateMessageLabel();
        _messageLabel.Location = new Point(20, 305);
        _messageLabel.MaximumSize = new Size(520, 0);

        var saveButton = new Button { Text = "Save", Location = new Point(310, 350) };
        UiTheme.StylePrimaryButton(saveButton);
        saveButton.Click += (_, _) => Save();

        var cancelButton = new Button { Text = "Cancel", Location = new Point(430, 350), DialogResult = DialogResult.Cancel };
        UiTheme.StyleSecondaryButton(cancelButton);

        AcceptButton = saveButton;
        CancelButton = cancelButton;
        Controls.AddRange([fields, _messageLabel, saveButton, cancelButton]);
    }

    private static Label InfoLabel(string text) => new()
    {
        AutoSize = true,
        Font = UiTheme.BodyBoldFont,
        Margin = new Padding(0, 8, 0, 4),
        Text = text,
    };

    private static TextBox MoneyBox(TableLayoutPanel fields, string caption, bool required)
    {
        TextBox box = FormFields.AddTextBox(fields, caption, 15, required);
        box.Dock = DockStyle.None;
        box.Width = 160;
        box.TextAlign = HorizontalAlignment.Right;
        return box;
    }

    private void ShowTotal()
    {
        bool rentOk = Money.TryParse(_rentBox.Text, out decimal rent);
        decimal transport = 0;
        bool transportOk = _transportBox.Text.Trim().Length == 0 || Money.TryParse(_transportBox.Text, out transport);
        if (!rentOk || !transportOk)
        {
            _totalLabel.Text = string.Empty;
            return;
        }
        decimal total = rent + transport;
        _totalLabel.Text = $"{Money.Format(total)}   (pending {Money.Format(Math.Max(total - _invoice.PaidAmount, 0))})";
    }

    private void Save()
    {
        if (!Money.TryParse(_rentBox.Text, out decimal rent))
        {
            FormFields.ShowError(_messageLabel, "Please enter the room rent for the year.");
            return;
        }
        decimal transport = 0;
        if (_transportBox.Text.Trim().Length > 0 && !Money.TryParse(_transportBox.Text, out transport))
        {
            FormFields.ShowError(_messageLabel, "Please enter the transport amount for the year, or leave it empty.");
            return;
        }

        try
        {
            InvoiceService.UpdateFee(_invoice.InvoiceId, new YearFee(rent, transport, _remarksBox.Text));
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (ValidationException ex)
        {
            FormFields.ShowError(_messageLabel, ex.Message);
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The fee could not be saved.");
        }
    }
}
