using HostelManagement.Models;
using HostelManagement.Services;
using HostelManagement.Utilities;

namespace HostelManagement.Forms;

/// <summary>
/// Corrects a payment entered wrongly: date, amount, method, reference and remarks, with an optional reason.
/// The receipt number stays the same and the change is kept in the change history. DialogResult.OK means saved.
/// </summary>
public sealed class PaymentEditForm : Form
{
    private readonly Payment _payment;
    private readonly DateTimePicker _datePicker;
    private readonly TextBox _amountBox;
    private readonly ComboBox _methodBox;
    private readonly TextBox _referenceBox;
    private readonly TextBox _remarksBox;
    private readonly TextBox _reasonBox;
    private readonly Label _messageLabel;

    public PaymentEditForm(Payment payment, Invoice invoice)
    {
        _payment = payment;

        Text = $"Edit Payment {payment.ReceiptNumber}";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Font;
        Font = UiTheme.BodyFont;
        BackColor = Color.White;
        ClientSize = new Size(640, 520);

        TableLayoutPanel fields = FormFields.CreateTable(labelWidth: 140, inputWidth: 460);
        FormFields.AddRow(fields, "Receipt", InfoLabel($"{payment.ReceiptNumber}   {payment.StudentName}"));
        decimal otherPayments = invoice.PaidAmount - payment.Amount;
        FormFields.AddRow(fields, "Invoice", InfoLabel(
            $"{invoice.InvoiceNumber}: fee {Money.Format(invoice.TotalAmount)}, other payments {Money.Format(otherPayments)}"));

        _datePicker = new DateTimePicker
        {
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "dd MMM yyyy",
            Width = 160,
            MaxDate = DateTime.Today,
            Value = payment.PaymentDate > DateTime.Today ? DateTime.Today : payment.PaymentDate,
            Margin = new Padding(0, 4, 0, 4),
        };
        FormFields.AddRow(fields, "Payment date", _datePicker, required: true);

        _amountBox = FormFields.AddTextBox(fields, "Amount", 15, required: true);
        _amountBox.TextAlign = HorizontalAlignment.Right;
        _amountBox.Dock = DockStyle.None;
        _amountBox.Width = 160;
        _amountBox.Text = payment.Amount.ToString("N2", Money.Culture);
        FormFields.AddRow(fields, string.Empty, new Label
        {
            AutoSize = true,
            ForeColor = UiTheme.TextMuted,
            Text = $"At most {Money.Format(invoice.TotalAmount - otherPayments)} (the fee less the other payments).",
        });

        _methodBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160, Margin = new Padding(0, 4, 0, 4) };
        _methodBox.Items.AddRange([.. PaymentMethod.All]);
        _methodBox.SelectedItem = payment.PaymentMethod;
        FormFields.AddRow(fields, "Paid by", _methodBox, required: true);

        _referenceBox = FormFields.AddTextBox(fields, "Reference", 100);
        _referenceBox.Text = payment.Reference;
        _remarksBox = FormFields.AddTextBox(fields, "Remarks", 255, multiline: true);
        _remarksBox.Text = payment.Remarks;
        _reasonBox = FormFields.AddTextBox(fields, "Reason for change", 255);
        _reasonBox.PlaceholderText = "For example: wrong amount typed";
        fields.Location = new Point(20, 20);

        _messageLabel = FormFields.CreateMessageLabel();
        _messageLabel.Location = new Point(20, 420);
        _messageLabel.MaximumSize = new Size(600, 0);

        var saveButton = new Button { Text = "Save", Location = new Point(390, 470) };
        UiTheme.StylePrimaryButton(saveButton);
        saveButton.Click += (_, _) => Save();
        var cancelButton = new Button { Text = "Cancel", Location = new Point(510, 470), DialogResult = DialogResult.Cancel };
        UiTheme.StyleSecondaryButton(cancelButton);

        AcceptButton = saveButton;
        CancelButton = cancelButton;
        Controls.AddRange([fields, _messageLabel, saveButton, cancelButton]);
    }

    /// <summary>The corrected payment, available after DialogResult.OK.</summary>
    public Payment? SavedPayment { get; private set; }

    private static Label InfoLabel(string text) => new()
    {
        AutoSize = true,
        Font = UiTheme.BodyBoldFont,
        Margin = new Padding(0, 8, 0, 4),
        Text = text,
    };

    private void Save()
    {
        if (!Money.TryParse(_amountBox.Text, out decimal amount))
        {
            FormFields.ShowError(_messageLabel, "Please enter a valid amount.");
            return;
        }

        try
        {
            SavedPayment = PaymentService.Update(new Payment
            {
                PaymentId = _payment.PaymentId,
                PaymentDate = _datePicker.Value.Date,
                Amount = amount,
                PaymentMethod = _methodBox.SelectedItem as string ?? string.Empty,
                Reference = _referenceBox.Text,
                Remarks = _remarksBox.Text,
            }, _reasonBox.Text);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (ValidationException ex)
        {
            FormFields.ShowError(_messageLabel, ex.Message);
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The payment could not be saved.");
        }
    }
}
