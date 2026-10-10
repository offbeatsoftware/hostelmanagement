using System.Diagnostics;
using HostelManagement.Models;
using HostelManagement.Reports;
using HostelManagement.Services;
using HostelManagement.Utilities;

namespace HostelManagement.Forms.Views;

/// <summary>Payments of the selected hostel: record, view/print and save receipts, delete a payment entered by mistake.</summary>
public sealed class PaymentsView : UserControl
{
    private const string AllMethods = "All methods";

    private readonly Hostel _hostel;
    private readonly TextBox _searchBox;
    private readonly ComboBox _methodFilter;
    private readonly DataGridView _grid;
    private readonly Button _viewButton;
    private readonly Button _saveButton;
    private readonly Button _emailButton;
    private readonly Button _deleteButton;
    private readonly Label _summaryLabel;

    private List<Payment> _payments = [];

    public PaymentsView(Hostel hostel)
    {
        _hostel = hostel;
        Dock = DockStyle.Fill;
        BackColor = UiTheme.ContentBackground;

        _searchBox = new TextBox
        {
            Font = UiTheme.BodyFont,
            Width = 220,
            PlaceholderText = "Search receipt, invoice or student",
            Margin = new Padding(0, 4, 8, 0),
        };
        _searchBox.TextChanged += (_, _) => ShowPayments();

        _methodFilter = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 130, Margin = new Padding(0, 4, 16, 0) };
        _methodFilter.Items.Add(AllMethods);
        _methodFilter.Items.AddRange([.. PaymentMethod.All]);
        _methodFilter.SelectedIndex = 0;
        _methodFilter.SelectedIndexChanged += (_, _) => ShowPayments();

        var recordButton = new Button { Text = "Record" };
        UiTheme.StylePrimaryButton(recordButton);
        recordButton.Click += (_, _) => RecordPayment();

        _viewButton = new Button { Text = "View / Print" };
        UiTheme.StyleSecondaryButton(_viewButton);
        _viewButton.Width = 120;
        _viewButton.Click += (_, _) => ViewSelectedReceipt();

        _saveButton = new Button { Text = "Save PDF" };
        UiTheme.StyleSecondaryButton(_saveButton);
        _saveButton.Click += (_, _) => SaveSelectedReceipt();

        _emailButton = new Button { Text = "Email Receipt" };
        UiTheme.StyleSecondaryButton(_emailButton);
        _emailButton.Width = 130;
        _emailButton.Click += async (_, _) => await EmailSelectedReceipt();

        _deleteButton = new Button { Text = "Delete" };
        UiTheme.StyleDangerButton(_deleteButton);
        _deleteButton.Click += (_, _) => DeleteSelectedPayment();

        _summaryLabel = FormFields.CreateMessageLabel();
        _summaryLabel.ForeColor = UiTheme.TextMuted;

        FlowLayoutPanel toolbar = FormFields.CreateButtonRow(
            _searchBox, _methodFilter, recordButton, _viewButton, _saveButton, _emailButton, _deleteButton, _summaryLabel);
        toolbar.Dock = DockStyle.Top;

        _grid = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false };
        UiTheme.StyleGrid(_grid);
        FormFields.AddGridColumn(_grid, nameof(Payment.ReceiptNumber), "Receipt no.", 14);
        FormFields.AddGridColumn(_grid, nameof(Payment.PaymentDate), "Date", 9, format: "dd MMM yyyy");
        FormFields.AddGridColumn(_grid, nameof(Payment.StudentName), "Student", 17);
        FormFields.AddGridColumn(_grid, nameof(Payment.InvoiceNumber), "Invoice no.", 13);
        FormFields.AddGridColumn(_grid, nameof(Payment.PaymentMethod), "Paid by", 9);
        FormFields.AddGridColumn(_grid, nameof(Payment.Reference), "Reference", 13);
        FormFields.AddGridColumn(_grid, nameof(Payment.Amount), "Amount", 11, format: "C2", alignRight: true);
        _grid.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0)
            {
                ViewSelectedReceipt();
            }
        };
        _grid.SelectionChanged += (_, _) => UpdateButtons();

        var body = new Panel { Dock = DockStyle.Fill };
        body.Controls.Add(_grid);
        body.Controls.Add(toolbar);
        Panel card = FormFields.CreateCard($"Payments of {hostel.HostelName}", body);
        card.Dock = DockStyle.Fill;
        Controls.Add(card);

        Load += (_, _) => LoadPayments();
    }

    private Payment? SelectedPayment => _grid.CurrentRow?.DataBoundItem as Payment;

    private void LoadPayments(int? selectPaymentId = null)
    {
        try
        {
            _payments = PaymentService.GetPayments(_hostel.HostelId);
            ShowPayments(selectPaymentId);
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The payments could not be loaded.");
        }
    }

    private void ShowPayments(int? selectPaymentId = null)
    {
        string search = _searchBox.Text.Trim();
        IEnumerable<Payment> payments = _payments;
        if (search.Length > 0)
        {
            payments = payments.Where(p =>
                p.ReceiptNumber.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                p.InvoiceNumber.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                p.Reference.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                p.StudentName.Contains(search, StringComparison.CurrentCultureIgnoreCase));
        }
        if (_methodFilter.SelectedItem is string method && method != AllMethods)
        {
            payments = payments.Where(p => p.PaymentMethod == method);
        }

        List<Payment> shown = payments.ToList();
        _grid.DataSource = shown;
        if (selectPaymentId is not null)
        {
            int index = shown.FindIndex(p => p.PaymentId == selectPaymentId);
            if (index >= 0)
            {
                _grid.CurrentCell = _grid.Rows[index].Cells[0];
            }
        }

        _summaryLabel.Text = $"{shown.Count} payments, received {Money.Format(shown.Sum(p => p.Amount))}";
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        bool hasSelection = SelectedPayment is not null;
        _viewButton.Enabled = hasSelection;
        _saveButton.Enabled = hasSelection;
        _emailButton.Enabled = hasSelection;
        _deleteButton.Enabled = hasSelection;
    }

    private void RecordPayment()
    {
        try
        {
            if (PaymentDialog.Show(this, _hostel) is Payment payment)
            {
                _searchBox.Clear();
                _methodFilter.SelectedIndex = 0;
                LoadPayments(payment.PaymentId);
            }
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The payment could not be recorded.");
        }
    }

    /// <summary>Writes the receipt PDF to the Receipts folder and opens it (print from the PDF viewer).</summary>
    private void ViewSelectedReceipt()
    {
        if (SelectedPayment is not Payment payment)
        {
            return;
        }

        try
        {
            string path = ReceiptPdfWriter.SaveToReceiptsFolder(PaymentService.GetReceiptData(payment.PaymentId));
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The receipt PDF could not be opened. Check that a PDF viewer is installed.");
        }
    }

    private void SaveSelectedReceipt()
    {
        if (SelectedPayment is not Payment payment)
        {
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Title = "Save receipt as PDF",
            Filter = "PDF file (*.pdf)|*.pdf",
            FileName = ReceiptPdfWriter.FileName(payment),
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            ReceiptPdfWriter.Write(PaymentService.GetReceiptData(payment.PaymentId), dialog.FileName);
            Dialogs.Info($"Receipt {payment.ReceiptNumber} was saved.");
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The receipt PDF could not be saved.");
        }
    }

    /// <summary>Emails the receipt PDF to the father, or the mother when the father has no email (only when the admin clicks the button).</summary>
    private async Task EmailSelectedReceipt()
    {
        if (SelectedPayment is not Payment payment)
        {
            return;
        }

        try
        {
            OutgoingEmail email = EmailService.PrepareReceipt(payment.PaymentId);
            if (Dialogs.Confirm($"Email receipt {payment.ReceiptNumber} to {email.RecipientName} ({email.RecipientEmail})?"))
            {
                await EmailSending.SendAsync(this, [email]);
            }
        }
        catch (ValidationException ex)
        {
            Dialogs.Warning(ex.Message);
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The receipt could not be emailed.");
        }
    }

    private void DeleteSelectedPayment()
    {
        if (SelectedPayment is not Payment payment ||
            !Dialogs.Confirm($"Delete payment {payment.ReceiptNumber} of {Money.Format(payment.Amount)} from {payment.StudentName}?\n\n" +
                             $"The amount becomes pending again on invoice {payment.InvoiceNumber}."))
        {
            return;
        }

        try
        {
            PaymentService.Delete(payment.PaymentId);
            LoadPayments();
        }
        catch (ValidationException ex)
        {
            Dialogs.Warning(ex.Message);
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The payment could not be deleted.");
        }
    }
}

/// <summary>Opens the Record Payment dialog for the hostel's invoices that still have an amount pending.</summary>
internal static class PaymentDialog
{
    /// <summary>Returns the saved payment, or null when cancelled or when nothing is pending.</summary>
    public static Payment? Show(IWin32Window owner, Hostel hostel, int? invoiceId = null)
    {
        List<Invoice> invoices = PaymentService.GetInvoicesWithPending(hostel.HostelId);
        if (invoices.Count == 0)
        {
            Dialogs.Info("There are no invoices with an amount pending. Create the invoice first on the Invoices screen.");
            return null;
        }

        using var dialog = new PaymentForm(invoices, invoiceId);
        return dialog.ShowDialog(owner) == DialogResult.OK ? dialog.SavedPayment : null;
    }
}
