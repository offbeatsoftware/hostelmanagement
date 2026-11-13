using HostelManagement.Models;
using HostelManagement.Services;
using HostelManagement.Utilities;

namespace HostelManagement.Forms;

/// <summary>
/// Refund (client decision, version 1.5): pay back all or part of what was paid on one of the student's fees, any
/// number of times, also after the student has left. DialogResult.OK means the refund was saved.
/// </summary>
public sealed class RefundForm : Form
{
    private readonly ComboBox _invoiceBox;
    private readonly Label _invoiceInfoLabel;
    private readonly DateTimePicker _datePicker;
    private readonly TextBox _amountBox;
    private readonly ComboBox _methodBox;
    private readonly TextBox _referenceBox;
    private readonly TextBox _reasonBox;
    private readonly Label _messageLabel;

    public RefundForm(Student student, IReadOnlyList<Invoice> invoices, int? invoiceId = null)
    {
        Text = $"Refund: {student.StudentName}";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Font;
        Font = UiTheme.BodyFont;
        BackColor = Color.White;
        ClientSize = new Size(660, 470);

        TableLayoutPanel fields = FormFields.CreateTable(labelWidth: 140, inputWidth: 480);
        _invoiceBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 4, 0, 4),
            DisplayMember = nameof(Invoice.InvoiceNumber),
            ValueMember = nameof(Invoice.InvoiceId),
            DataSource = invoices.ToList(),
        };
        FormFields.AddRow(fields, "From fee (invoice)", _invoiceBox, required: true);
        _invoiceInfoLabel = new Label { AutoSize = true, MaximumSize = new Size(480, 0), ForeColor = UiTheme.TextMuted, Margin = new Padding(0, 2, 0, 6) };
        FormFields.AddRow(fields, string.Empty, _invoiceInfoLabel);

        _datePicker = new DateTimePicker
        {
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "dd MMM yyyy",
            Width = 160,
            MaxDate = DateTime.Today,
            Value = DateTime.Today,
            Margin = new Padding(0, 4, 0, 4),
        };
        FormFields.AddRow(fields, "Refund date", _datePicker, required: true);

        _amountBox = FormFields.AddTextBox(fields, "Amount", 15, required: true);
        _amountBox.TextAlign = HorizontalAlignment.Right;
        _amountBox.Dock = DockStyle.None;
        _amountBox.Width = 160;

        _methodBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160, Margin = new Padding(0, 4, 0, 4) };
        _methodBox.Items.AddRange([.. PaymentMethod.All]);
        _methodBox.SelectedIndex = 0;
        FormFields.AddRow(fields, "Paid back by", _methodBox, required: true);
        _referenceBox = FormFields.AddTextBox(fields, "Reference", 100);
        _reasonBox = FormFields.AddTextBox(fields, "Reason", 255, required: true, multiline: true);
        _reasonBox.PlaceholderText = "For example: left the hostel in December";

        _messageLabel = FormFields.CreateMessageLabel();
        _messageLabel.MaximumSize = new Size(380, 0);
        var saveButton = new Button { Text = "Save Refund" };
        UiTheme.StylePrimaryButton(saveButton);
        saveButton.Width = 130;
        saveButton.Click += (_, _) => Save();
        var cancelButton = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
        UiTheme.StyleSecondaryButton(cancelButton);
        AcceptButton = saveButton;
        CancelButton = cancelButton;

        var note = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(620, 0),
            ForeColor = UiTheme.TextMuted,
            Margin = new Padding(0, 6, 0, 6),
            Text = "A refund pays money back and lowers the fee by the same amount, so the pending amount does not change. " +
                   "It can be at most what is paid on the fee after earlier refunds.",
        };
        FlowLayoutPanel stack = FormFields.CreateStack(fields, note, FormFields.CreateButtonRow(saveButton, cancelButton, _messageLabel));
        stack.Location = new Point(18, 16);
        Controls.Add(stack);

        Load += (_, _) =>
        {
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

    public Refund? SavedRefund { get; private set; }

    private Invoice? SelectedInvoice => _invoiceBox.SelectedItem as Invoice;

    private string Method => _methodBox.SelectedItem as string ?? PaymentMethod.Cash;

    private void ShowInvoice()
    {
        if (SelectedInvoice is not Invoice invoice)
        {
            _invoiceInfoLabel.Text = string.Empty;
            return;
        }
        _invoiceInfoLabel.Text = $"Fee {invoice.YearText}: {Money.Format(invoice.TotalAmount)}, paid {Money.Format(invoice.PaidAmount)}" +
                                 (invoice.RefundedAmount > 0 ? $" (after {Money.Format(invoice.RefundedAmount)} refunded)" : "") +
                                 $", pending {Money.Format(invoice.PendingAmount)}. Can refund up to {Money.Format(invoice.PaidAmount)}.";
        _amountBox.Text = string.Empty;
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
            FormFields.ShowError(_messageLabel, "Please select the fee to refund from.");
            return;
        }
        if (!Money.TryParse(_amountBox.Text, out decimal amount))
        {
            FormFields.ShowError(_messageLabel, "Please enter the amount as a number, for example 5000.");
            _amountBox.Focus();
            return;
        }
        if (!Dialogs.Confirm($"Refund {Money.Format(amount)} from invoice {invoice.InvoiceNumber}?"))
        {
            return;
        }

        try
        {
            SavedRefund = RefundService.Record(new Refund
            {
                InvoiceId = invoice.InvoiceId,
                RefundDate = _datePicker.Value.Date,
                Amount = amount,
                RefundMethod = Method,
                Reference = _referenceBox.Text,
                Reason = _reasonBox.Text,
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
            ErrorHandler.Handle(ex, "The refund could not be saved.");
        }
    }
}
