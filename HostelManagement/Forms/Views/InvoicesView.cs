using System.Diagnostics;
using HostelManagement.Models;
using HostelManagement.Reports;
using HostelManagement.Services;
using HostelManagement.Utilities;

namespace HostelManagement.Forms.Views;

/// <summary>
/// Yearly invoices of the selected hostel (one per student per academic year, created at check-in or with
/// New Year Fees): edit the fee, record a payment, view/print as PDF, save PDF, email and delete.
/// </summary>
public sealed class InvoicesView : UserControl
{
    private const string AllStatuses = "All invoices";
    private const string AllYears = "All years";

    private readonly Hostel _hostel;
    private readonly TextBox _searchBox;
    private readonly ComboBox _statusFilter;
    private readonly ComboBox _yearFilter;
    private readonly DataGridView _grid;
    private readonly Button _editFeeButton;
    private readonly Button _payButton;
    private readonly Button _viewButton;
    private readonly Button _saveButton;
    private readonly Button _emailButton;
    private readonly Button _deleteButton;
    private readonly Label _summaryLabel;

    private List<Invoice> _invoices = [];

    public InvoicesView(Hostel hostel)
    {
        _hostel = hostel;
        Dock = DockStyle.Fill;
        BackColor = UiTheme.ContentBackground;

        _searchBox = new TextBox
        {
            Font = UiTheme.BodyFont,
            Width = 220,
            PlaceholderText = "Search invoice no. or student",
            Margin = new Padding(0, 4, 8, 0),
        };
        _searchBox.TextChanged += (_, _) => ShowInvoices();

        _statusFilter = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 130, Margin = new Padding(0, 4, 16, 0) };
        _statusFilter.Items.AddRange([AllStatuses, InvoiceStatus.Unpaid, InvoiceStatus.PartlyPaid, InvoiceStatus.Paid]);
        _statusFilter.SelectedIndex = 0;
        _statusFilter.SelectedIndexChanged += (_, _) => ShowInvoices();

        _yearFilter = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110, Margin = new Padding(0, 4, 16, 0) };
        _yearFilter.Items.Add(AllYears);
        _yearFilter.SelectedIndex = 0;
        _yearFilter.SelectedIndexChanged += (_, _) => ShowInvoices();

        var newYearButton = new Button { Text = "New Year Fees" };
        UiTheme.StylePrimaryButton(newYearButton);
        newYearButton.Width = 140;
        newYearButton.Click += (_, _) => CreateNewYearFees();

        _editFeeButton = new Button { Text = "Edit Fee" };
        UiTheme.StyleSecondaryButton(_editFeeButton);
        _editFeeButton.Click += (_, _) => EditSelectedFee();

        _payButton = new Button { Text = "Record Payment" };
        UiTheme.StyleSecondaryButton(_payButton);
        _payButton.Width = 140;
        _payButton.Click += (_, _) => RecordPayment();

        _viewButton = new Button { Text = "View / Print" };
        UiTheme.StyleSecondaryButton(_viewButton);
        _viewButton.Width = 120;
        _viewButton.Click += (_, _) => ViewSelectedInvoice();

        _saveButton = new Button { Text = "Save PDF" };
        UiTheme.StyleSecondaryButton(_saveButton);
        _saveButton.Click += (_, _) => SaveSelectedInvoice();

        _emailButton = new Button { Text = "Email" };
        UiTheme.StyleSecondaryButton(_emailButton);
        _emailButton.Click += async (_, _) => await EmailSelectedInvoice();

        _deleteButton = new Button { Text = "Delete" };
        UiTheme.StyleDangerButton(_deleteButton);
        _deleteButton.Click += (_, _) => DeleteSelectedInvoice();

        _summaryLabel = FormFields.CreateMessageLabel();
        _summaryLabel.ForeColor = UiTheme.TextMuted;

        FlowLayoutPanel filterRow = FormFields.CreateButtonRow(_searchBox, _statusFilter, _yearFilter, _summaryLabel);
        filterRow.Dock = DockStyle.Top;
        FlowLayoutPanel toolbar = FormFields.CreateButtonRow(
            newYearButton, _editFeeButton, _payButton, _viewButton, _saveButton, _emailButton, _deleteButton);
        toolbar.Dock = DockStyle.Top;

        _grid = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false };
        UiTheme.StyleGrid(_grid);
        FormFields.AddGridColumn(_grid, nameof(Invoice.InvoiceNumber), "Invoice no.", 13);
        FormFields.AddGridColumn(_grid, nameof(Invoice.YearText), "Year", 7);
        FormFields.AddGridColumn(_grid, nameof(Invoice.StudentName), "Student", 17);
        FormFields.AddGridColumn(_grid, nameof(Invoice.RoomRent), "Room rent", 10, format: "C2", alignRight: true);
        FormFields.AddGridColumn(_grid, nameof(Invoice.TransportAmount), "Transport", 9, format: "C2", alignRight: true);
        FormFields.AddGridColumn(_grid, nameof(Invoice.TotalAmount), "Total fee", 10, format: "C2", alignRight: true);
        FormFields.AddGridColumn(_grid, nameof(Invoice.PaidAmount), "Paid", 10, format: "C2", alignRight: true);
        FormFields.AddGridColumn(_grid, nameof(Invoice.PendingAmount), "Pending", 10, format: "C2", alignRight: true);
        FormFields.AddGridColumn(_grid, nameof(Invoice.Status), "Status", 8);
        FormFields.AddGridColumn(_grid, nameof(Invoice.InvoiceDate), "Date", 8, format: "dd MMM yyyy");
        _grid.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0)
            {
                ViewSelectedInvoice();
            }
        };
        _grid.SelectionChanged += (_, _) => UpdateButtons();

        var body = new Panel { Dock = DockStyle.Fill };
        body.Controls.Add(_grid);
        body.Controls.Add(toolbar);
        body.Controls.Add(filterRow);
        Panel card = FormFields.CreateCard($"Invoices of {hostel.HostelName}", body);
        card.Dock = DockStyle.Fill;
        Controls.Add(card);

        Load += (_, _) => LoadInvoices();
    }

    private Invoice? SelectedInvoice => _grid.CurrentRow?.DataBoundItem as Invoice;

    private void LoadInvoices(int? selectInvoiceId = null)
    {
        try
        {
            _invoices = InvoiceService.GetInvoices(_hostel.HostelId);
            FillYearFilter();
            ShowInvoices(selectInvoiceId);
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The invoices could not be loaded.");
        }
    }

    /// <summary>The academic years that have invoices, newest first, keeping the current choice.</summary>
    private void FillYearFilter()
    {
        object? current = _yearFilter.SelectedItem;
        _yearFilter.BeginUpdate();
        _yearFilter.Items.Clear();
        _yearFilter.Items.Add(AllYears);
        _yearFilter.Items.AddRange(_invoices.Select(i => i.YearText).Distinct().OrderDescending().Cast<object>().ToArray());
        _yearFilter.SelectedItem = current is not null && _yearFilter.Items.Contains(current) ? current : AllYears;
        _yearFilter.EndUpdate();
    }

    private void ShowInvoices(int? selectInvoiceId = null)
    {
        string search = _searchBox.Text.Trim();
        IEnumerable<Invoice> invoices = _invoices;
        if (search.Length > 0)
        {
            invoices = invoices.Where(i =>
                i.InvoiceNumber.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                i.StudentName.Contains(search, StringComparison.CurrentCultureIgnoreCase));
        }
        if (_statusFilter.SelectedItem is string status && status != AllStatuses)
        {
            invoices = invoices.Where(i => i.Status == status);
        }
        if (_yearFilter.SelectedItem is string year && year != AllYears)
        {
            invoices = invoices.Where(i => i.YearText == year);
        }

        List<Invoice> shown = invoices.ToList();
        _grid.DataSource = shown;
        if (selectInvoiceId is not null)
        {
            int index = shown.FindIndex(i => i.InvoiceId == selectInvoiceId);
            if (index >= 0)
            {
                _grid.CurrentCell = _grid.Rows[index].Cells[0];
            }
        }

        _summaryLabel.Text = $"{shown.Count} invoices, total {Money.Format(shown.Sum(i => i.TotalAmount))}, " +
                             $"paid {Money.Format(shown.Sum(i => i.PaidAmount))}, pending {Money.Format(shown.Sum(i => i.PendingAmount))}";
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        bool hasSelection = SelectedInvoice is not null;
        _editFeeButton.Enabled = hasSelection;
        _payButton.Enabled = SelectedInvoice is { PendingAmount: > 0 };
        _viewButton.Enabled = hasSelection;
        _saveButton.Enabled = hasSelection;
        _emailButton.Enabled = hasSelection;
        _deleteButton.Enabled = hasSelection;
    }

    /// <summary>Enters the fees of a new academic year for the students staying on.</summary>
    private void CreateNewYearFees()
    {
        try
        {
            using var dialog = new NewYearFeesForm(_hostel);
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                _searchBox.Clear();
                LoadInvoices(dialog.CreatedInvoices.FirstOrDefault()?.InvoiceId);
            }
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The new year fees could not be created.");
        }
    }

    /// <summary>Changes the room rent or transport agreed for the selected invoice's year.</summary>
    private void EditSelectedFee()
    {
        if (SelectedInvoice is not Invoice invoice)
        {
            return;
        }

        try
        {
            using var dialog = new FeeEditForm(invoice);
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                LoadInvoices(invoice.InvoiceId);
            }
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The fee could not be changed.");
        }
    }

    private void RecordPayment()
    {
        if (SelectedInvoice is not Invoice invoice)
        {
            return;
        }

        try
        {
            if (PaymentDialog.Show(this, _hostel, invoice.InvoiceId) is not null)
            {
                LoadInvoices(invoice.InvoiceId);
            }
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The payment could not be recorded.");
        }
    }

    /// <summary>Writes the invoice PDF to the Invoices folder and opens it (print from the PDF viewer).</summary>
    private void ViewSelectedInvoice()
    {
        if (SelectedInvoice is not Invoice invoice)
        {
            return;
        }

        try
        {
            string path = InvoicePdfWriter.SaveToInvoicesFolder(InvoiceService.GetPrintData(invoice.InvoiceId));
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The invoice PDF could not be opened. Check that a PDF viewer is installed.");
        }
    }

    private void SaveSelectedInvoice()
    {
        if (SelectedInvoice is not Invoice invoice)
        {
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Title = "Save invoice as PDF",
            Filter = "PDF file (*.pdf)|*.pdf",
            FileName = InvoicePdfWriter.FileName(invoice),
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            InvoicePdfWriter.Write(InvoiceService.GetPrintData(invoice.InvoiceId), dialog.FileName);
            Dialogs.Info($"Invoice {invoice.InvoiceNumber} was saved.");
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The invoice PDF could not be saved.");
        }
    }

    /// <summary>Emails the invoice PDF to the father (or the mother when the father has no email).</summary>
    private async Task EmailSelectedInvoice()
    {
        if (SelectedInvoice is not Invoice invoice)
        {
            return;
        }

        try
        {
            OutgoingEmail email = EmailService.PrepareInvoice(invoice.InvoiceId);
            if (Dialogs.Confirm($"Email invoice {invoice.InvoiceNumber} to {email.RecipientName} ({email.RecipientEmail})?"))
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
            ErrorHandler.Handle(ex, "The invoice could not be emailed.");
        }
    }

    private void DeleteSelectedInvoice()
    {
        if (SelectedInvoice is not Invoice invoice ||
            !Dialogs.Confirm($"Delete invoice {invoice.InvoiceNumber} for {invoice.StudentName}?"))
        {
            return;
        }

        try
        {
            InvoiceService.Delete(invoice.InvoiceId);
            LoadInvoices();
        }
        catch (ValidationException ex)
        {
            Dialogs.Warning(ex.Message);
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The invoice could not be deleted.");
        }
    }
}
