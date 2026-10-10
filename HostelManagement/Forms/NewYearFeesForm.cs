using System.Globalization;
using HostelManagement.Models;
using HostelManagement.Services;
using HostelManagement.Utilities;

namespace HostelManagement.Forms;

/// <summary>
/// New Year Fees: every student in a room who has no fee for the chosen academic year, with last year's amounts
/// for reference. The admin types the new room rent and transport (client decision: a new fee every year);
/// students left empty are skipped. DialogResult.OK means invoices were created.
/// </summary>
public sealed class NewYearFeesForm : Form
{
    private const string RentColumn = "Rent";
    private const string TransportColumn = "Transport";

    private readonly Hostel _hostel;
    private readonly ComboBox _yearBox;
    private readonly DataGridView _grid;
    private readonly Label _messageLabel;
    private List<NewYearFeeCandidate> _candidates = [];

    public NewYearFeesForm(Hostel hostel)
    {
        _hostel = hostel;

        Text = "New Year Fees";
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Font;
        Font = UiTheme.BodyFont;
        BackColor = Color.White;
        ClientSize = new Size(900, 560);
        MinimumSize = new Size(760, 420);

        var yearLabel = new Label { Text = "Academic year", AutoSize = true, Margin = new Padding(0, 8, 8, 0) };
        _yearBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110, Margin = new Padding(0, 4, 16, 0) };
        int current = AcademicYear.Current;
        _yearBox.Items.AddRange([AcademicYear.Label(current), AcademicYear.Label(current + 1)]);
        // From April the next academic year is the one being prepared.
        _yearBox.SelectedIndex = DateTime.Today.Month is >= 4 and < AcademicYear.StartMonth ? 1 : 0;
        _yearBox.SelectedIndexChanged += (_, _) => LoadCandidates();
        var note = new Label
        {
            AutoSize = true,
            ForeColor = UiTheme.TextMuted,
            Margin = new Padding(0, 8, 0, 0),
            Text = "Type the room rent and transport for each student staying on. Leave a row empty to skip the student.",
        };
        FlowLayoutPanel top = FormFields.CreateButtonRow(yearLabel, _yearBox, note);
        top.Dock = DockStyle.Top;

        _grid = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false };
        UiTheme.StyleGrid(_grid);
        _grid.ReadOnly = false;
        _grid.EditMode = DataGridViewEditMode.EditOnEnter;
        _grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
        AddColumn("Student", "Student", 24, readOnly: true);
        AddColumn("Room", "Room", 10, readOnly: true);
        AddColumn("LastRent", "Last year rent", 14, readOnly: true, alignRight: true);
        AddColumn("LastTransport", "Last year transport", 14, readOnly: true, alignRight: true);
        AddColumn(RentColumn, "Room rent / year", 19, readOnly: false, alignRight: true);
        AddColumn(TransportColumn, "Transport / year", 19, readOnly: false, alignRight: true);

        _messageLabel = FormFields.CreateMessageLabel();
        _messageLabel.MaximumSize = new Size(560, 0);
        var createButton = new Button { Text = "Create Fees" };
        UiTheme.StylePrimaryButton(createButton);
        createButton.Width = 130;
        createButton.Click += (_, _) => CreateFees();
        var cancelButton = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
        UiTheme.StyleSecondaryButton(cancelButton);
        FlowLayoutPanel bottom = FormFields.CreateButtonRow(createButton, cancelButton, _messageLabel);
        bottom.Dock = DockStyle.Bottom;
        CancelButton = cancelButton;

        var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };
        body.Controls.Add(_grid);
        body.Controls.Add(top);
        body.Controls.Add(bottom);
        Controls.Add(body);

        Load += (_, _) => LoadCandidates();
    }

    /// <summary>The invoices created, available after DialogResult.OK.</summary>
    public List<Invoice> CreatedInvoices { get; private set; } = [];

    private int SelectedYear => AcademicYear.Current + _yearBox.SelectedIndex;

    private void AddColumn(string name, string header, int weight, bool readOnly, bool alignRight = false)
    {
        var column = new DataGridViewTextBoxColumn
        {
            Name = name,
            HeaderText = header,
            FillWeight = weight,
            ReadOnly = readOnly,
            SortMode = DataGridViewColumnSortMode.NotSortable,
        };
        if (alignRight)
        {
            column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        }
        if (!readOnly)
        {
            column.DefaultCellStyle.BackColor = Color.FromArgb(255, 251, 235);
        }
        _grid.Columns.Add(column);
    }

    private void LoadCandidates()
    {
        try
        {
            _candidates = InvoiceService.GetNewYearCandidates(_hostel.HostelId, SelectedYear);
            _grid.Rows.Clear();
            foreach (NewYearFeeCandidate candidate in _candidates)
            {
                _grid.Rows.Add(candidate.Student.StudentName, candidate.RoomText,
                    candidate.LastYear is null ? "" : Money.Format(candidate.LastYear.RoomRent),
                    candidate.LastYear is null ? "" : Money.Format(candidate.LastYear.TransportAmount),
                    "", "");
            }
            _messageLabel.ForeColor = UiTheme.TextMuted;
            _messageLabel.Text = _candidates.Count == 0
                ? $"Every student in a room already has a fee for {AcademicYear.Label(SelectedYear)}."
                : $"{_candidates.Count} students without a fee for {AcademicYear.Label(SelectedYear)}.";
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The students could not be loaded.");
        }
    }

    private void CreateFees()
    {
        _grid.EndEdit();
        var fees = new Dictionary<int, YearFee>();
        for (int i = 0; i < _candidates.Count; i++)
        {
            string rentText = Convert.ToString(_grid.Rows[i].Cells[RentColumn].Value, CultureInfo.CurrentCulture)?.Trim() ?? "";
            string transportText = Convert.ToString(_grid.Rows[i].Cells[TransportColumn].Value, CultureInfo.CurrentCulture)?.Trim() ?? "";
            if (rentText.Length == 0 && transportText.Length == 0)
            {
                continue;
            }

            string name = _candidates[i].Student.StudentName;
            if (!Money.TryParse(rentText, out decimal rent) || rent <= 0)
            {
                FormFields.ShowError(_messageLabel, $"Please enter a valid room rent for {name}.");
                _grid.CurrentCell = _grid.Rows[i].Cells[RentColumn];
                return;
            }
            decimal transport = 0;
            if (transportText.Length > 0 && !Money.TryParse(transportText, out transport))
            {
                FormFields.ShowError(_messageLabel, $"Please enter a valid transport amount for {name}, or leave it empty.");
                _grid.CurrentCell = _grid.Rows[i].Cells[TransportColumn];
                return;
            }
            fees[_candidates[i].Student.StudentId] = new YearFee(rent, transport);
        }

        if (fees.Count == 0)
        {
            FormFields.ShowError(_messageLabel, "Please enter the room rent for at least one student.");
            return;
        }
        if (!Dialogs.Confirm($"Create the {AcademicYear.Label(SelectedYear)} fee for {fees.Count} student(s)?"))
        {
            return;
        }

        try
        {
            CreatedInvoices = InvoiceService.CreateForYear(SelectedYear, fees);
            Dialogs.Info($"{CreatedInvoices.Count} invoice(s) created for {AcademicYear.Label(SelectedYear)}.");
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (ValidationException ex)
        {
            FormFields.ShowError(_messageLabel, ex.Message);
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The fees could not be created.");
        }
    }
}
