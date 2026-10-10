using System.Diagnostics;
using HostelManagement.Models;
using HostelManagement.Reports;
using HostelManagement.Services;
using HostelManagement.Utilities;

namespace HostelManagement.Forms.Views;

/// <summary>
/// Reports of the selected hostel: choose a report and its filters, check it on screen,
/// then export it as PDF or Excel.
/// </summary>
public sealed class ReportsView : UserControl
{
    private const string Students = "Student list";
    private const string Rooms = "Room occupancy";
    private const string Payments = "Payments received";
    private const string InvoiceList = "Fees (invoices)";
    private const string Dues = "Pending fees";

    private readonly Hostel _hostel;
    private readonly ListBox _reportList;
    private readonly DateTimePicker _fromPicker;
    private readonly DateTimePicker _toPicker;
    private readonly ComboBox _studentStatusBox;
    private readonly ComboBox _collegeBox;
    private readonly ComboBox _methodBox;
    private readonly ComboBox _invoiceStatusBox;
    private readonly ComboBox _yearBox;
    private readonly Dictionary<Control, string[]> _filterReports = [];
    private readonly DataGridView _grid;
    private readonly Label _messageLabel;
    private readonly Button _pdfButton;
    private readonly Button _excelButton;

    private ReportTable? _report;

    public ReportsView(Hostel hostel)
    {
        _hostel = hostel;
        Dock = DockStyle.Fill;
        BackColor = UiTheme.ContentBackground;

        _reportList = new ListBox
        {
            Dock = DockStyle.Fill,
            Font = UiTheme.BodyFont,
            BorderStyle = BorderStyle.None,
            ItemHeight = 28,
            DrawMode = DrawMode.OwnerDrawFixed,
        };
        _reportList.Items.AddRange(new object[] { Students, Rooms, Payments, InvoiceList, Dues });
        _reportList.DrawItem += DrawReportItem;
        Panel listCard = FormFields.CreateCard("Report", _reportList);
        listCard.Dock = DockStyle.Left;
        listCard.Width = 170;

        // ---- Filters (only those of the chosen report are shown) ----
        DateTime today = DateTime.Today;
        _fromPicker = DatePicker(new DateTime(today.Year, today.Month, 1));
        _toPicker = DatePicker(today);

        _studentStatusBox = Combo(130, [ReportService.AllOption, StudentStatus.Active, StudentStatus.Left]);
        _studentStatusBox.SelectedItem = StudentStatus.Active;

        _collegeBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 220,
            Margin = new Padding(0, 4, 12, 0),
            DisplayMember = nameof(College.CollegeName),
            ValueMember = nameof(College.CollegeId),
        };

        _methodBox = Combo(130, [ReportService.AllOption, .. PaymentMethod.All]);
        _invoiceStatusBox = Combo(130,
            [ReportService.AllOption, InvoiceStatus.Unpaid, InvoiceStatus.PartlyPaid, InvoiceStatus.Paid]);
        // This academic year first, then the next and the earlier ones.
        int year = AcademicYear.Current;
        _yearBox = Combo(110,
            [AcademicYear.Label(year), AcademicYear.Label(year + 1), .. Enumerable.Range(1, 5).Select(i => AcademicYear.Label(year - i)),
             ReportService.AllOption]);

        var filters = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, WrapContents = false, Padding = new Padding(0, 4, 0, 0) };
        AddFilter(filters, "From", _fromPicker, Payments);
        AddFilter(filters, "To", _toPicker, Payments);
        AddFilter(filters, "Year", _yearBox, InvoiceList);
        AddFilter(filters, "Status", _studentStatusBox, Students);
        AddFilter(filters, "College", _collegeBox, Students);
        AddFilter(filters, "Paid by", _methodBox, Payments);
        AddFilter(filters, "Status", _invoiceStatusBox, InvoiceList);

        var showButton = new Button { Text = "Show" };
        UiTheme.StylePrimaryButton(showButton);
        showButton.Click += (_, _) => ShowReport();

        _pdfButton = new Button { Text = "Export PDF" };
        UiTheme.StyleSecondaryButton(_pdfButton);
        _pdfButton.Width = 120;
        _pdfButton.Click += (_, _) => Export(pdf: true);

        _excelButton = new Button { Text = "Export Excel" };
        UiTheme.StyleSecondaryButton(_excelButton);
        _excelButton.Width = 120;
        _excelButton.Click += (_, _) => Export(pdf: false);

        _messageLabel = FormFields.CreateMessageLabel();
        _messageLabel.ForeColor = UiTheme.TextMuted;
        FlowLayoutPanel buttons = FormFields.CreateButtonRow(showButton, _pdfButton, _excelButton, _messageLabel);
        buttons.Dock = DockStyle.Top;

        _grid = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false };
        UiTheme.StyleGrid(_grid);
        // Columns as wide as their content, with a horizontal scroll bar for wide reports.
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
        _grid.ScrollBars = ScrollBars.Both;

        var body = new Panel { Dock = DockStyle.Fill };
        body.Controls.Add(_grid);
        body.Controls.Add(buttons);
        body.Controls.Add(filters);
        Panel reportCard = FormFields.CreateCard($"Reports of {hostel.HostelName}", body);
        reportCard.Dock = DockStyle.Fill;

        var gap = new Panel { Dock = DockStyle.Left, Width = 10 };
        Controls.Add(reportCard);
        Controls.Add(gap);
        Controls.Add(listCard);

        Load += (_, _) =>
        {
            LoadColleges();
            _reportList.SelectedIndexChanged += (_, _) => ReportChanged();
            _reportList.SelectedIndex = 0;
        };
    }

    private string SelectedReport => _reportList.SelectedItem as string ?? Students;

    private static DateTimePicker DatePicker(DateTime value) => new()
    {
        Format = DateTimePickerFormat.Custom,
        CustomFormat = "dd MMM yyyy",
        Width = 130,
        Value = value,
        Margin = new Padding(0, 4, 12, 0),
    };

    private static ComboBox Combo(int width, object[] items)
    {
        var box = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = width, Margin = new Padding(0, 4, 12, 0) };
        box.Items.AddRange(items);
        box.SelectedIndex = 0;
        return box;
    }

    /// <summary>Adds a filter with its caption; it is shown only for the listed reports.</summary>
    private void AddFilter(FlowLayoutPanel panel, string? caption, Control input, params string[] reports)
    {
        if (caption is not null)
        {
            var label = new Label { Text = caption, AutoSize = true, Margin = new Padding(0, 8, 6, 0), ForeColor = UiTheme.TextPrimary };
            panel.Controls.Add(label);
            _filterReports[label] = reports;
        }
        panel.Controls.Add(input);
        _filterReports[input] = reports;
    }

    private void LoadColleges()
    {
        try
        {
            var colleges = new List<College> { new() { CollegeId = 0, CollegeName = "All colleges" } };
            colleges.AddRange(CollegeService.Search(_hostel.HostelId));
            _collegeBox.DataSource = colleges;
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The colleges could not be loaded.");
        }
    }

    private void ReportChanged()
    {
        foreach ((Control control, string[] reports) in _filterReports)
        {
            control.Visible = reports.Contains(SelectedReport);
        }
        _reportList.Invalidate();
        ShowReport();
    }

    private static string? Choice(ComboBox box) => box.SelectedItem is string text && text != ReportService.AllOption ? text : null;

    private ReportTable BuildReport() => SelectedReport switch
    {
        Students => ReportService.StudentList(_hostel, Choice(_studentStatusBox),
            _collegeBox.SelectedValue is int collegeId && collegeId > 0 ? collegeId : null),
        Rooms => ReportService.RoomOccupancy(_hostel),
        Payments => ReportService.PaymentsReceived(_hostel, _fromPicker.Value.Date, _toPicker.Value.Date, Choice(_methodBox)),
        InvoiceList => ReportService.Invoices(_hostel, Choice(_yearBox) is string label ? int.Parse(label[..4], System.Globalization.CultureInfo.InvariantCulture) : null,
            Choice(_invoiceStatusBox)),
        _ => ReportService.PendingDues(_hostel, DateTime.Today),
    };

    /// <summary>Builds the chosen report and shows it in the grid; totals and group lines are bold.</summary>
    private void ShowReport()
    {
        _report = null;
        _grid.Columns.Clear();
        _grid.Rows.Clear();
        _pdfButton.Enabled = _excelButton.Enabled = false;

        try
        {
            ReportTable report = BuildReport();
            foreach (ReportColumn column in report.Columns)
            {
                var gridColumn = new DataGridViewTextBoxColumn
                {
                    HeaderText = column.Header,
                    SortMode = DataGridViewColumnSortMode.NotSortable,
                };
                if (column.Kind is ReportValueKind.Money or ReportValueKind.Number)
                {
                    gridColumn.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                }
                _grid.Columns.Add(gridColumn);
            }
            foreach (ReportRow row in report.Rows)
            {
                int index = _grid.Rows.Add(row.Values.Select((v, i) => (object)ReportService.FormatValue(v, report.Columns[i].Kind)).ToArray());
                if (row.Style != ReportRowStyle.Normal)
                {
                    _grid.Rows[index].DefaultCellStyle.Font = UiTheme.BodyBoldFont;
                }
                if (row.Style is ReportRowStyle.Subtotal or ReportRowStyle.Total)
                {
                    _grid.Rows[index].DefaultCellStyle.BackColor = UiTheme.ContentBackground;
                }
            }
            _grid.ClearSelection();

            _report = report;
            _pdfButton.Enabled = _excelButton.Enabled = true;
            _messageLabel.Text = string.Empty;
        }
        catch (ValidationException ex)
        {
            FormFields.ShowError(_messageLabel, ex.Message);
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The report could not be created.");
        }
    }

    /// <summary>Saves the report shown (rebuilt with the current filters) and opens the file.</summary>
    private void Export(bool pdf)
    {
        ShowReport();
        if (_report is not ReportTable report)
        {
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Title = pdf ? "Export report as PDF" : "Export report as Excel",
            Filter = pdf ? "PDF file (*.pdf)|*.pdf" : "Excel workbook (*.xlsx)|*.xlsx",
            InitialDirectory = AppPaths.ReportsFolder,
            FileName = report.FileName + (pdf ? ".pdf" : ".xlsx"),
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            if (pdf)
            {
                ReportPdfWriter.Write(report, dialog.FileName);
            }
            else
            {
                ReportExcelWriter.Write(report, dialog.FileName);
            }
            Process.Start(new ProcessStartInfo(dialog.FileName) { UseShellExecute = true });
        }
        catch (IOException ex)
        {
            ErrorHandler.Handle(ex, "The file could not be saved. If it is open in Excel or a PDF viewer, close it and try again.");
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The report could not be exported.");
        }
    }

    /// <summary>Report names drawn like the menu: the chosen one in terracotta.</summary>
    private void DrawReportItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0)
        {
            return;
        }
        bool selected = (e.State & DrawItemState.Selected) != 0;
        using var background = new SolidBrush(selected ? UiTheme.NavSelected : Color.White);
        e.Graphics.FillRectangle(background, e.Bounds);
        TextRenderer.DrawText(e.Graphics, _reportList.Items[e.Index].ToString(), UiTheme.BodyFont,
            new Rectangle(e.Bounds.X + 8, e.Bounds.Y, e.Bounds.Width - 8, e.Bounds.Height),
            selected ? Color.White : UiTheme.TextPrimary, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
    }
}
