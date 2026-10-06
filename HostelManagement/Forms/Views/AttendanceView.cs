using System.ComponentModel;
using HostelManagement.Models;
using HostelManagement.Services;
using HostelManagement.Utilities;

namespace HostelManagement.Forms.Views;

/// <summary>
/// Night attendance of the selected hostel: choose the date, untick the absent students, save; then email the
/// parents of the absent students. Earlier dates can be opened and corrected.
/// </summary>
public sealed class AttendanceView : UserControl
{
    private readonly Hostel _hostel;
    private readonly DateTimePicker _datePicker;
    private readonly Label _statusLabel;
    private readonly DataGridView _sheetGrid;
    private readonly DataGridView _absentGrid;
    private readonly Button _saveButton;
    private readonly Button _emailButton;
    private readonly Label _absentNote;

    private BindingList<AttendanceEntry> _entries = new();
    private DateTime _shownDate;
    private bool _dirty;

    public AttendanceView(Hostel hostel)
    {
        _hostel = hostel;
        Dock = DockStyle.Fill;
        BackColor = UiTheme.ContentBackground;

        _datePicker = new DateTimePicker
        {
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "dddd, dd MMM yyyy",
            Width = 210,
            MaxDate = DateTime.Today,
            Value = DateTime.Today,
            Margin = new Padding(0, 4, 8, 0),
        };

        var todayButton = new Button { Text = "Today" };
        UiTheme.StyleSecondaryButton(todayButton);
        todayButton.Width = 80;
        todayButton.Click += (_, _) => _datePicker.Value = DateTime.Today;

        var allPresentButton = new Button { Text = "Mark All Present" };
        UiTheme.StyleSecondaryButton(allPresentButton);
        allPresentButton.Width = 150;
        allPresentButton.Click += (_, _) => MarkAllPresent();

        _saveButton = new Button { Text = "Save Attendance" };
        UiTheme.StylePrimaryButton(_saveButton);
        _saveButton.Width = 150;
        _saveButton.Click += (_, _) => SaveAttendance();

        _statusLabel = FormFields.CreateMessageLabel();

        FlowLayoutPanel toolbar = FormFields.CreateButtonRow(_datePicker, todayButton, allPresentButton, _saveButton, _statusLabel);
        toolbar.Dock = DockStyle.Top;

        // ---- Sheet: everyone in a room that night ----
        _sheetGrid = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false };
        UiTheme.StyleGrid(_sheetGrid);
        _sheetGrid.ReadOnly = false;
        _sheetGrid.EditMode = DataGridViewEditMode.EditOnEnter;
        FormFields.AddGridColumn(_sheetGrid, nameof(AttendanceEntry.RoomAndBed), "Room", 14).ReadOnly = true;
        FormFields.AddGridColumn(_sheetGrid, nameof(AttendanceEntry.StudentName), "Student", 30).ReadOnly = true;
        _sheetGrid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            DataPropertyName = nameof(AttendanceEntry.IsPresent),
            Name = nameof(AttendanceEntry.IsPresent),
            HeaderText = "Present",
            FillWeight = 10,
        });
        DataGridViewTextBoxColumn remarks = FormFields.AddGridColumn(_sheetGrid, nameof(AttendanceEntry.Remarks), "Remarks", 36);
        remarks.MaxInputLength = 255;
        // A ticked or unticked box counts at once, not only when the cell is left.
        _sheetGrid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_sheetGrid.IsCurrentCellDirty && _sheetGrid.CurrentCell is DataGridViewCheckBoxCell)
            {
                _sheetGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        };
        _sheetGrid.CellValueChanged += (_, e) =>
        {
            if (e.RowIndex >= 0)
            {
                _dirty = true;
                ShowStatus();
            }
        };
        _sheetGrid.CellFormatting += (_, e) =>
        {
            if (e.RowIndex >= 0 && e.RowIndex < _entries.Count && !_entries[e.RowIndex].IsPresent && e.CellStyle is not null)
            {
                e.CellStyle.ForeColor = UiTheme.Danger;
            }
        };
        Panel sheetCard = FormFields.CreateCard("Night attendance (untick the students who are not in the hostel)", _sheetGrid);
        sheetCard.Dock = DockStyle.Fill;
        sheetCard.Margin = new Padding(0, 0, 6, 0);

        // ---- Absent students and emails to their parents ----
        _absentGrid = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false };
        UiTheme.StyleGrid(_absentGrid);
        FormFields.AddGridColumn(_absentGrid, nameof(AttendanceEntry.StudentName), "Student", 28);
        FormFields.AddGridColumn(_absentGrid, nameof(AttendanceEntry.RoomNumber), "Room", 12);
        FormFields.AddGridColumn(_absentGrid, nameof(AttendanceEntry.ParentMobile), "Parent mobile", 24);
        FormFields.AddGridColumn(_absentGrid, nameof(AttendanceEntry.EmailedText), "Parent", 26);

        _emailButton = new Button { Text = "Email Parents" };
        UiTheme.StylePrimaryButton(_emailButton);
        _emailButton.Width = 140;
        _emailButton.Click += async (_, _) => await EmailParents();
        _absentNote = FormFields.CreateMessageLabel();
        _absentNote.ForeColor = UiTheme.TextMuted;
        FlowLayoutPanel absentButtons = FormFields.CreateButtonRow(_emailButton, _absentNote);
        absentButtons.Dock = DockStyle.Bottom;

        var absentBody = new Panel { Dock = DockStyle.Fill };
        absentBody.Controls.Add(_absentGrid);
        absentBody.Controls.Add(absentButtons);
        Panel absentCard = FormFields.CreateCard("Absent students", absentBody);
        absentCard.Dock = DockStyle.Fill;
        absentCard.Margin = new Padding(6, 0, 0, 0);

        var columns = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
        columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        columns.Controls.Add(sheetCard, 0, 0);
        columns.Controls.Add(absentCard, 1, 0);

        Controls.Add(columns);
        Controls.Add(toolbar);

        Load += (_, _) =>
        {
            LoadSheet(DateTime.Today);
            _datePicker.ValueChanged += (_, _) => DateChanged();
        };
    }

    private void DateChanged()
    {
        DateTime date = _datePicker.Value.Date;
        if (date == _shownDate)
        {
            return;
        }
        if (_dirty && !Dialogs.Confirm($"The attendance of {_shownDate:dd MMM yyyy} has not been saved. Discard the changes?"))
        {
            _datePicker.Value = _shownDate;
            return;
        }
        LoadSheet(date);
    }

    private void LoadSheet(DateTime date)
    {
        try
        {
            AttendanceSheet sheet = AttendanceService.GetSheet(_hostel.HostelId, date);
            ShowSheet(sheet);
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The attendance could not be loaded.");
        }
    }

    private void ShowSheet(AttendanceSheet sheet)
    {
        _shownDate = sheet.Date;
        _entries = new BindingList<AttendanceEntry>(sheet.Entries);
        _sheetGrid.DataSource = _entries;
        _dirty = false;
        ShowStatus(sheet.IsMarked);
        ShowAbsent();
    }

    private void ShowStatus(bool? marked = null)
    {
        int absent = _entries.Count(e => !e.IsPresent);
        bool isMarked = marked ?? _entries.Any(e => e.IsSaved);
        string state = _dirty ? "Not saved yet" : isMarked ? "Saved" : "Not marked yet";
        _statusLabel.ForeColor = _dirty || !isMarked ? UiTheme.Danger : UiTheme.Success;
        _statusLabel.Text = $"{state}: {_entries.Count - absent} present, {absent} absent";
        _saveButton.Enabled = _entries.Count > 0;
    }

    /// <summary>The saved absences of the date; emails can only be sent for saved attendance.</summary>
    private void ShowAbsent()
    {
        List<AttendanceEntry> absent = _entries.Where(e => e.IsSaved && !e.IsPresent).ToList();
        _absentGrid.DataSource = absent;
        int notEmailed = absent.Count(e => e.ParentEmailedDate is null);
        _emailButton.Enabled = absent.Count > 0;
        _absentNote.Text = absent.Count == 0
            ? "No absent students saved for this date."
            : notEmailed == 0 ? "All parents have been emailed." : $"{notEmailed} parent(s) not emailed yet.";
    }

    private void MarkAllPresent()
    {
        _sheetGrid.EndEdit();
        foreach (AttendanceEntry entry in _entries.Where(e => !e.IsPresent))
        {
            entry.IsPresent = true;
            _dirty = true;
        }
        _entries.ResetBindings();
        ShowStatus();
    }

    private bool SaveAttendance()
    {
        _sheetGrid.EndEdit();
        try
        {
            ShowSheet(AttendanceService.Save(_hostel.HostelId, _shownDate, _entries.ToList()));
            return true;
        }
        catch (ValidationException ex)
        {
            Dialogs.Warning(ex.Message);
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The attendance could not be saved.");
        }
        return false;
    }

    /// <summary>Emails the parents of the absent students who have not been emailed for this date yet.</summary>
    private async Task EmailParents()
    {
        try
        {
            if (_dirty)
            {
                if (!Dialogs.Confirm("Save the attendance first?") || !SaveAttendance())
                {
                    return;
                }
            }

            (List<OutgoingEmail> emails, List<string> problems) = EmailService.PrepareAbsences(_hostel.HostelId, _shownDate);
            if (emails.Count == 0 && problems.Count == 0)
            {
                if (!Dialogs.Confirm("The parents of all absent students have already been emailed for this date. Email them again?"))
                {
                    return;
                }
                (emails, problems) = EmailService.PrepareAbsences(_hostel.HostelId, _shownDate, includeAlreadyEmailed: true);
            }
            else if (emails.Count > 0 &&
                     !Dialogs.Confirm($"Email the parents of {emails.Count} absent student(s) about {_shownDate:dd MMM yyyy}?"))
            {
                return;
            }

            await EmailSending.SendAsync(this, emails, problems);
            LoadSheet(_shownDate);
        }
        catch (ValidationException ex)
        {
            Dialogs.Warning(ex.Message);
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The emails could not be sent.");
        }
    }
}
