using System.Diagnostics;
using HostelManagement.Models;
using HostelManagement.Reports;
using HostelManagement.Services;
using HostelManagement.Utilities;

namespace HostelManagement.Forms.Views;

/// <summary>Invoices of the selected hostel: create, view/print as PDF, save PDF and delete.</summary>
public sealed class InvoicesView : UserControl
{
    private const string AllStatuses = "All invoices";

    private readonly Hostel _hostel;
    private readonly TextBox _searchBox;
    private readonly ComboBox _statusFilter;
    private readonly DataGridView _grid;
    private readonly Button _viewButton;
    private readonly Button _saveButton;
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

        var createButton = new Button { Text = "Create" };
        UiTheme.StylePrimaryButton(createButton);
        createButton.Click += (_, _) => CreateInvoice();

        _viewButton = new Button { Text = "View / Print" };
        UiTheme.StyleSecondaryButton(_viewButton);
        _viewButton.Width = 120;
        _viewButton.Click += (_, _) => ViewSelectedInvoice();

        _saveButton = new Button { Text = "Save PDF" };
        UiTheme.StyleSecondaryButton(_saveButton);
        _saveButton.Click += (_, _) => SaveSelectedInvoice();

        _deleteButton = new Button { Text = "Delete" };
        UiTheme.StyleDangerButton(_deleteButton);
        _deleteButton.Click += (_, _) => DeleteSelectedInvoice();

        _summaryLabel = FormFields.CreateMessageLabel();
        _summaryLabel.ForeColor = UiTheme.TextMuted;

        FlowLayoutPanel toolbar = FormFields.CreateButtonRow(
            _searchBox, _statusFilter, createButton, _viewButton, _saveButton, _deleteButton, _summaryLabel);
        toolbar.Dock = DockStyle.Top;

        _grid = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false };
        UiTheme.StyleGrid(_grid);
        FormFields.AddGridColumn(_grid, nameof(Invoice.InvoiceNumber), "Invoice no.", 13);
        FormFields.AddGridColumn(_grid, nameof(Invoice.InvoiceDate), "Date", 9, format: "dd MMM yyyy");
        FormFields.AddGridColumn(_grid, nameof(Invoice.StudentName), "Student", 18);
        FormFields.AddGridColumn(_grid, nameof(Invoice.PeriodText), "Billing period", 15);
        FormFields.AddGridColumn(_grid, nameof(Invoice.TotalAmount), "Total", 11, format: "C2", alignRight: true);
        FormFields.AddGridColumn(_grid, nameof(Invoice.PaidAmount), "Paid", 11, format: "C2", alignRight: true);
        FormFields.AddGridColumn(_grid, nameof(Invoice.PendingAmount), "Pending", 11, format: "C2", alignRight: true);
        FormFields.AddGridColumn(_grid, nameof(Invoice.Status), "Status", 9);
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
            ShowInvoices(selectInvoiceId);
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The invoices could not be loaded.");
        }
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

        _summaryLabel.Text = $"{shown.Count} invoices, pending {Money.Format(shown.Sum(i => i.PendingAmount))}";
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        bool hasSelection = SelectedInvoice is not null;
        _viewButton.Enabled = hasSelection;
        _saveButton.Enabled = hasSelection;
        _deleteButton.Enabled = hasSelection;
    }

    private void CreateInvoice()
    {
        try
        {
            List<Student> students = StudentService.GetStudents(_hostel.HostelId);
            if (students.Count == 0)
            {
                Dialogs.Info("There are no students in this hostel yet.");
                return;
            }

            using var dialog = new InvoiceCreateForm(_hostel, students);
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                _searchBox.Clear();
                LoadInvoices(dialog.CreatedInvoice?.InvoiceId);
            }
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The invoice could not be created.");
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
