using HostelManagement.Models;
using HostelManagement.Services;
using HostelManagement.Utilities;

namespace HostelManagement.Forms;

/// <summary>Record a payment against an invoice that still has an amount pending. DialogResult.OK means it was saved.</summary>
public sealed class PaymentForm : Form
{
    private readonly ComboBox _invoiceBox;
    private readonly Label _invoiceInfoLabel;
    private readonly DateTimePicker _datePicker;
    private readonly TextBox _amountBox;
    private readonly ComboBox _methodBox;
    private readonly TextBox _referenceBox;
    private readonly TextBox _remarksBox;
    private readonly Label _messageLabel;

    public PaymentForm(IReadOnlyList<Invoice> invoices, int? invoiceId = null)
    {
        Text = "Record Payment";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Font;
        Font = UiTheme.BodyFont;
        BackColor = Color.White;
        ClientSize = new Size(640, 440);

        TableLayoutPanel fields = FormFields.CreateTable(labelWidth: 130, inputWidth: 470);
        _invoiceBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 4, 0, 4),
            DisplayMember = nameof(Invoice.PickerText),
            ValueMember = nameof(Invoice.InvoiceId),
            DataSource = invoices.ToList(),
            TabIndex = 0,
        };
        FormFields.AddRow(fields, "Invoice", _invoiceBox, required: true);

        _invoiceInfoLabel = new Label { AutoSize = true, ForeColor = UiTheme.TextMuted, Margin = new Padding(0, 2, 0, 6) };
        FormFields.AddRow(fields, string.Empty, _invoiceInfoLabel);

        _datePicker = new DateTimePicker
        {
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "dd MMM yyyy",
            Width = 160,
            MaxDate = DateTime.Today,
            Value = DateTime.Today,
            Margin = new Padding(0, 4, 0, 4),
            TabIndex = 1,
        };
        FormFields.AddRow(fields, "Payment date", _datePicker, required: true);

        _amountBox = FormFields.AddTextBox(fields, "Amount", 15, required: true);
        _amountBox.TextAlign = HorizontalAlignment.Right;
        _amountBox.Dock = DockStyle.None;
        _amountBox.Width = 160;

        _methodBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 160,
            Margin = new Padding(0, 4, 0, 4),
            TabIndex = fields.Controls.Count,
        };
        _methodBox.Items.AddRange([.. PaymentMethod.All]);
        _methodBox.SelectedIndex = 0;
        FormFields.AddRow(fields, "Paid by", _methodBox, required: true);

        _referenceBox = FormFields.AddTextBox(fields, "Reference", 100);
        _remarksBox = FormFields.AddTextBox(fields, "Remarks", 255, multiline: true);
        fields.Location = new Point(20, 20);

        _messageLabel = FormFields.CreateMessageLabel();
        _messageLabel.Location = new Point(20, 330);
        _messageLabel.MaximumSize = new Size(600, 0);

        var saveButton = new Button { Text = "Save", Location = new Point(390, 390) };
        UiTheme.StylePrimaryButton(saveButton);
        saveButton.Click += (_, _) => Save();

        var cancelButton = new Button { Text = "Cancel", Location = new Point(510, 390) };
        UiTheme.StyleSecondaryButton(cancelButton);
        cancelButton.DialogResult = DialogResult.Cancel;

        AcceptButton = saveButton;
        CancelButton = cancelButton;

        Controls.AddRange([fields, _messageLabel, saveButton, cancelButton]);

        Load += (_, _) =>
        {
            // The invoice list is filled by data binding once the form exists.
            if (invoiceId is not null)
            {
                _invoiceBox.SelectedValue = invoiceId.Value;
            }
            _invoiceBox.SelectedIndexChanged += (_, _) => ShowInvoice();
            _methodBox.SelectedIndexChanged += (_, _) => ShowReferenceHint();
            ShowInvoice();
            ShowReferenceHint();
        };
    }

    public Payment? SavedPayment { get; private set; }

    private Invoice? SelectedInvoice => _invoiceBox.SelectedItem as Invoice;

    private string Method => _methodBox.SelectedItem as string ?? PaymentMethod.Cash;

    /// <summary>Shows the invoice's amounts and suggests paying the full pending amount.</summary>
    private void ShowInvoice()
    {
        if (SelectedInvoice is not Invoice invoice)
        {
            _invoiceInfoLabel.Text = string.Empty;
            return;
        }

        _invoiceInfoLabel.Text = $"{invoice.PeriodText}:  total {Money.Format(invoice.TotalAmount)},  " +
                                 $"paid {Money.Format(invoice.PaidAmount)},  pending {Money.Format(invoice.PendingAmount)}";
        _amountBox.Text = invoice.PendingAmount.ToString("N2", Money.Culture);
    }

    private void ShowReferenceHint()
    {
        string name = PaymentMethod.ReferenceName(Method);
        _referenceBox.PlaceholderText = PaymentMethod.RequiresReference(Method) ? $"{name} (required)" : "Optional for cash";
    }

    private void Save()
    {
        _messageLabel.Text = string.Empty;
        if (SelectedInvoice is not Invoice invoice)
        {
            FormFields.ShowError(_messageLabel, "Please select the invoice.");
            return;
        }
        if (!Money.TryParse(_amountBox.Text, out decimal amount))
        {
            FormFields.ShowError(_messageLabel, "Please enter the amount as a number, for example 25000.");
            _amountBox.Focus();
            return;
        }

        try
        {
            SavedPayment = PaymentService.Record(new Payment
            {
                InvoiceId = invoice.InvoiceId,
                PaymentDate = _datePicker.Value.Date,
                Amount = amount,
                PaymentMethod = Method,
                Reference = _referenceBox.Text,
                Remarks = _remarksBox.Text,
            });
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
