using System.Diagnostics;
using HostelManagement.Models;
using HostelManagement.Reports;
using HostelManagement.Services;
using HostelManagement.Utilities;

namespace HostelManagement.Forms.Views;

/// <summary>
/// Pending dues of the selected hostel: one row per student with the amount pending and how long it is
/// overdue, the selected student's unpaid invoices below, recording a payment and exporting the list as PDF.
/// </summary>
public sealed class PendingDuesView : UserControl
{
    private readonly Hostel _hostel;
    private readonly TextBox _searchBox;
    private readonly CheckBox _overdueOnlyBox;
    private readonly DataGridView _studentsGrid;
    private readonly DataGridView _invoicesGrid;
    private readonly Label _invoicesTitle;
    private readonly Button _payButton;
    private readonly Label _summaryLabel;

    private List<StudentDue> _dues = [];

    public PendingDuesView(Hostel hostel)
    {
        _hostel = hostel;
        Dock = DockStyle.Fill;
        BackColor = UiTheme.ContentBackground;

        _searchBox = new TextBox
        {
            Font = UiTheme.BodyFont,
            Width = 220,
            PlaceholderText = "Search student, room or parent",
            Margin = new Padding(0, 4, 8, 0),
        };

        _overdueOnlyBox = new CheckBox { Text = "Overdue only", AutoSize = true, Margin = new Padding(0, 7, 16, 0) };

        _payButton = new Button { Text = "Record Payment" };
        UiTheme.StylePrimaryButton(_payButton);
        _payButton.Width = 140;
        _payButton.Click += (_, _) => RecordPayment();

        var exportButton = new Button { Text = "Export PDF" };
        UiTheme.StyleSecondaryButton(exportButton);
        exportButton.Width = 110;
        exportButton.Click += (_, _) => ExportPdf();

        _summaryLabel = FormFields.CreateMessageLabel();
        _summaryLabel.ForeColor = UiTheme.TextMuted;

        FlowLayoutPanel toolbar = FormFields.CreateButtonRow(_searchBox, _overdueOnlyBox, _payButton, exportButton, _summaryLabel);
        toolbar.Dock = DockStyle.Top;

        _studentsGrid = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false };
        UiTheme.StyleGrid(_studentsGrid);
        FormFields.AddGridColumn(_studentsGrid, nameof(StudentDue.StudentName), "Student", 17);
        FormFields.AddGridColumn(_studentsGrid, nameof(StudentDue.StudentStatus), "Status", 7);
        FormFields.AddGridColumn(_studentsGrid, nameof(StudentDue.RoomNumber), "Room", 7);
        FormFields.AddGridColumn(_studentsGrid, nameof(StudentDue.ParentText), "Parent / mobile", 18);
        FormFields.AddGridColumn(_studentsGrid, nameof(StudentDue.InvoiceCount), "Invoices", 7, alignRight: true);
        FormFields.AddGridColumn(_studentsGrid, nameof(StudentDue.PendingAmount), "Pending", 11, format: "C2", alignRight: true);
        FormFields.AddGridColumn(_studentsGrid, nameof(StudentDue.OverdueAmount), "Overdue", 11, format: "C2", alignRight: true);
        FormFields.AddGridColumn(_studentsGrid, nameof(StudentDue.DueText), "Due", 12);
        _studentsGrid.CellFormatting += (_, e) =>
        {
            if (e.RowIndex >= 0 && _studentsGrid.Rows[e.RowIndex].DataBoundItem is StudentDue { IsOverdue: true } &&
                e.CellStyle is not null)
            {
                e.CellStyle.ForeColor = UiTheme.Danger;
            }
        };
        // CurrentCellChanged (not SelectionChanged) fires after CurrentRow points to the newly chosen student.
        _studentsGrid.CurrentCellChanged += (_, _) => ShowInvoices();

        _invoicesTitle = new Label { Dock = DockStyle.Top, Height = 30, Font = UiTheme.BodyBoldFont, Padding = new Padding(0, 8, 0, 0) };

        _invoicesGrid = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false };
        UiTheme.StyleGrid(_invoicesGrid);
        FormFields.AddGridColumn(_invoicesGrid, nameof(Invoice.InvoiceNumber), "Invoice no.", 14);
        FormFields.AddGridColumn(_invoicesGrid, nameof(Invoice.InvoiceDate), "Date", 10, format: "dd MMM yyyy");
        FormFields.AddGridColumn(_invoicesGrid, nameof(Invoice.PeriodText), "Billing period", 16);
        FormFields.AddGridColumn(_invoicesGrid, nameof(Invoice.DueDate), "Due date", 10, format: "dd MMM yyyy");
        FormFields.AddGridColumn(_invoicesGrid, nameof(Invoice.TotalAmount), "Total", 11, format: "C2", alignRight: true);
        FormFields.AddGridColumn(_invoicesGrid, nameof(Invoice.PaidAmount), "Paid", 11, format: "C2", alignRight: true);
        FormFields.AddGridColumn(_invoicesGrid, nameof(Invoice.PendingAmount), "Pending", 11, format: "C2", alignRight: true);
        FormFields.AddGridColumn(_invoicesGrid, nameof(Invoice.Status), "Status", 9);
        _invoicesGrid.CellFormatting += (_, e) =>
        {
            if (e.RowIndex >= 0 && _invoicesGrid.Rows[e.RowIndex].DataBoundItem is Invoice { IsOverdue: true } &&
                e.CellStyle is not null)
            {
                e.CellStyle.ForeColor = UiTheme.Danger;
            }
        };
        _invoicesGrid.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0)
            {
                RecordPayment();
            }
        };

        var invoicesPanel = new Panel { Dock = DockStyle.Fill };
        invoicesPanel.Controls.Add(_invoicesGrid);
        invoicesPanel.Controls.Add(_invoicesTitle);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 60));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
        layout.Controls.Add(_studentsGrid, 0, 0);
        layout.Controls.Add(invoicesPanel, 0, 1);

        var body = new Panel { Dock = DockStyle.Fill };
        body.Controls.Add(layout);
        body.Controls.Add(toolbar);
        Panel card = FormFields.CreateCard($"Pending dues of {hostel.HostelName} (payment due {Invoice.PaymentDueDays} days after the invoice date)", body);
        card.Dock = DockStyle.Fill;
        Controls.Add(card);

        // Attached last so they do not fire while the screen is still being built.
        _searchBox.TextChanged += (_, _) => ShowDues();
        _overdueOnlyBox.CheckedChanged += (_, _) => ShowDues();
        Load += (_, _) => LoadDues();
    }

    private StudentDue? SelectedDue => _studentsGrid.CurrentRow?.DataBoundItem as StudentDue;

    /// <summary>The students currently shown (after search and filter), as exported to PDF.</summary>
    private List<StudentDue> ShownDues => (_studentsGrid.DataSource as List<StudentDue>) ?? [];

    private void LoadDues(int? selectStudentId = null)
    {
        try
        {
            _dues = PendingDuesService.GetDues(_hostel.HostelId, DateTime.Today);
            ShowDues(selectStudentId);
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The pending dues could not be loaded.");
        }
    }

    private void ShowDues(int? selectStudentId = null)
    {
        string search = _searchBox.Text.Trim();
        IEnumerable<StudentDue> dues = _dues;
        if (search.Length > 0)
        {
            dues = dues.Where(d =>
                d.StudentName.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
                d.RoomNumber.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                d.ParentText.Contains(search, StringComparison.CurrentCultureIgnoreCase));
        }
        if (_overdueOnlyBox.Checked)
        {
            dues = dues.Where(d => d.IsOverdue);
        }

        List<StudentDue> shown = dues.ToList();
        _studentsGrid.DataSource = shown;
        if (selectStudentId is not null)
        {
            int index = shown.FindIndex(d => d.StudentId == selectStudentId);
            if (index >= 0)
            {
                _studentsGrid.CurrentCell = _studentsGrid.Rows[index].Cells[0];
            }
        }

        _summaryLabel.Text = $"{shown.Count} students, pending {Money.Format(shown.Sum(d => d.PendingAmount))}, " +
                             $"overdue {Money.Format(shown.Sum(d => d.OverdueAmount))}";
        ShowInvoices();
    }

    private void ShowInvoices()
    {
        // The students grid also reports a change while the screen is being closed.
        if (IsDisposed || Disposing || _invoicesGrid.IsDisposed)
        {
            return;
        }

        StudentDue? due = SelectedDue;
        _invoicesGrid.DataSource = due?.Invoices;
        _invoicesTitle.Text = due is null ? "Unpaid invoices" : $"Unpaid invoices of {due.StudentName}";
        _payButton.Enabled = due is not null;
    }

    /// <summary>Records a payment for the selected invoice, or the student's oldest unpaid invoice.</summary>
    private void RecordPayment()
    {
        if (SelectedDue is not StudentDue due)
        {
            return;
        }
        Invoice invoice = _invoicesGrid.CurrentRow?.DataBoundItem as Invoice ?? due.Invoices[0];

        try
        {
            if (PaymentDialog.Show(this, _hostel, invoice.InvoiceId) is not null)
            {
                LoadDues(due.StudentId);
            }
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The payment could not be recorded.");
        }
    }

    /// <summary>Saves the students shown (after search and filter) as a PDF and opens it.</summary>
    private void ExportPdf()
    {
        using var dialog = new SaveFileDialog
        {
            Title = "Export pending dues as PDF",
            Filter = "PDF file (*.pdf)|*.pdf",
            InitialDirectory = AppPaths.ReportsFolder,
            FileName = PendingDuesPdfWriter.FileName(_hostel, DateTime.Today),
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            PendingDuesPdfWriter.Write(_hostel, ShownDues, DateTime.Today, dialog.FileName);
            Process.Start(new ProcessStartInfo(dialog.FileName) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The pending dues PDF could not be saved or opened.");
        }
    }
}
