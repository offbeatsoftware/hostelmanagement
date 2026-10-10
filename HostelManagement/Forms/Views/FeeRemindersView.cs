using System.Diagnostics;
using HostelManagement.Models;
using HostelManagement.Reports;
using HostelManagement.Services;
using HostelManagement.Utilities;

namespace HostelManagement.Forms.Views;

/// <summary>
/// Fee Reminders (client decision, version 1.2): every student of the selected hostel with a pending fee, with
/// total, paid and pending amounts. The admin ticks the students, checks the ready-made reminder text (it can be
/// changed for this sending, or saved as the new default) and sends it to the father, or to the mother when
/// the father has no email. Reminders are only sent when the admin clicks Send, never automatically.
/// </summary>
public sealed class FeeRemindersView : UserControl
{
    private readonly Hostel _hostel;
    private readonly TextBox _searchBox;
    private readonly DataGridView _grid;
    private readonly Button _sendButton;
    private readonly Button _payButton;
    private readonly Label _summaryLabel;
    private readonly TextBox _subjectBox;
    private readonly TextBox _bodyBox;
    private readonly Label _previewTitle;
    private readonly TextBox _previewBox;
    private readonly Label _textMessage;

    private List<ReminderRow> _rows = [];

    public FeeRemindersView(Hostel hostel)
    {
        _hostel = hostel;
        Dock = DockStyle.Fill;
        BackColor = UiTheme.ContentBackground;

        // ---- Students ----
        _searchBox = new TextBox
        {
            Font = UiTheme.BodyFont,
            Width = 200,
            PlaceholderText = "Search student, room or father",
            Margin = new Padding(0, 4, 8, 0),
        };
        var selectAllButton = new Button { Text = "Select All" };
        UiTheme.StyleSecondaryButton(selectAllButton);
        selectAllButton.Click += (_, _) => SetAllTicks(true);
        var clearButton = new Button { Text = "Clear" };
        UiTheme.StyleSecondaryButton(clearButton);
        clearButton.Width = 80;
        clearButton.Click += (_, _) => SetAllTicks(false);

        _sendButton = new Button { Text = "Send Reminder" };
        UiTheme.StylePrimaryButton(_sendButton);
        _sendButton.Width = 190;
        _sendButton.Click += async (_, _) => await SendReminders();

        _payButton = new Button { Text = "Record Payment" };
        UiTheme.StyleSecondaryButton(_payButton);
        _payButton.Width = 140;
        _payButton.Click += (_, _) => RecordPayment();

        var exportButton = new Button { Text = "Export PDF" };
        UiTheme.StyleSecondaryButton(exportButton);
        exportButton.Click += (_, _) => ExportPdf();

        _summaryLabel = FormFields.CreateMessageLabel();
        _summaryLabel.ForeColor = UiTheme.TextMuted;

        FlowLayoutPanel filterRow = FormFields.CreateButtonRow(_searchBox, selectAllButton, clearButton, _summaryLabel);
        filterRow.Dock = DockStyle.Top;
        FlowLayoutPanel buttonRow = FormFields.CreateButtonRow(_sendButton, _payButton, exportButton);
        buttonRow.Dock = DockStyle.Top;

        _grid = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false };
        UiTheme.StyleGrid(_grid);
        _grid.ReadOnly = false;
        _grid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            DataPropertyName = nameof(ReminderRow.Send),
            HeaderText = "Send",
            FillWeight = 5,
            SortMode = DataGridViewColumnSortMode.NotSortable,
        });
        AddReadOnlyColumn(nameof(ReminderRow.StudentName), "Student", 17);
        AddReadOnlyColumn(nameof(ReminderRow.RoomNumber), "Room", 6);
        AddReadOnlyColumn(nameof(ReminderRow.Years), "Year", 7);
        AddReadOnlyColumn(nameof(ReminderRow.TotalAmount), "Total fee", 10, "C0");
        AddReadOnlyColumn(nameof(ReminderRow.PaidAmount), "Paid", 10, "C0");
        AddReadOnlyColumn(nameof(ReminderRow.PendingAmount), "Pending", 10, "C0");
        AddReadOnlyColumn(nameof(ReminderRow.EmailText), "Email goes to", 21);
        AddReadOnlyColumn(nameof(ReminderRow.LastReminderDate), "Last reminder", 10, "dd MMM yyyy");
        _grid.CellFormatting += (_, e) =>
        {
            if (e.RowIndex >= 0 && _grid.Rows[e.RowIndex].DataBoundItem is ReminderRow { CanEmail: false } && e.CellStyle is not null)
            {
                e.CellStyle.ForeColor = UiTheme.Danger;
            }
        };
        // Tick boxes take effect at once, not only when the cell is left.
        _grid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_grid.IsCurrentCellDirty)
            {
                _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        };
        _grid.CellValueChanged += (_, _) => UpdateButtons();
        // CurrentCellChanged (not SelectionChanged) fires after CurrentRow points to the newly chosen student.
        _grid.CurrentCellChanged += (_, _) => ShowPreview();

        var studentsBody = new Panel { Dock = DockStyle.Fill };
        studentsBody.Controls.Add(_grid);
        studentsBody.Controls.Add(buttonRow);
        studentsBody.Controls.Add(filterRow);
        Panel studentsCard = FormFields.CreateCard($"Pending fees of {hostel.HostelName}: tick the students to remind", studentsBody);
        studentsCard.Dock = DockStyle.Fill;
        studentsCard.Margin = new Padding(0, 0, 8, 0);

        // ---- Email text and preview ----
        var subjectLabel = new Label { Text = "Subject", Dock = DockStyle.Top, Height = 22 };
        _subjectBox = new TextBox { Dock = DockStyle.Top, Font = UiTheme.BodyFont, MaxLength = 255 };
        var bodyLabel = new Label { Text = "Message", Dock = DockStyle.Top, Height = 24, Padding = new Padding(0, 6, 0, 0) };
        _bodyBox = new TextBox
        {
            Dock = DockStyle.Top,
            Height = 180,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            AcceptsReturn = true,
            Font = UiTheme.BodyFont,
        };
        var fieldsLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 36,
            ForeColor = UiTheme.TextMuted,
            Text = "Fields: " + string.Join(" ", EmailSettingsService.Fields[EmailType.DueReminder].Select(f => "{" + f + "}")),
        };
        var saveTextButton = new Button { Text = "Save as Default" };
        UiTheme.StyleSecondaryButton(saveTextButton);
        saveTextButton.Width = 140;
        saveTextButton.Click += (_, _) => SaveText();
        var restoreButton = new Button { Text = "Saved Text" };
        UiTheme.StyleSecondaryButton(restoreButton);
        restoreButton.Click += (_, _) => LoadText();
        _textMessage = FormFields.CreateMessageLabel();
        FlowLayoutPanel textButtons = FormFields.CreateButtonRow(saveTextButton, restoreButton, _textMessage);
        textButtons.Dock = DockStyle.Top;

        _previewTitle = new Label { Dock = DockStyle.Top, Height = 28, Font = UiTheme.BodyBoldFont, Padding = new Padding(0, 8, 0, 0) };
        _previewBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            BackColor = Color.White,
            Font = UiTheme.BodyFont,
        };

        var emailBody = new Panel { Dock = DockStyle.Fill };
        // Docked controls are laid out in reverse order of adding.
        emailBody.Controls.Add(_previewBox);
        emailBody.Controls.Add(_previewTitle);
        emailBody.Controls.Add(textButtons);
        emailBody.Controls.Add(fieldsLabel);
        emailBody.Controls.Add(_bodyBox);
        emailBody.Controls.Add(bodyLabel);
        emailBody.Controls.Add(_subjectBox);
        emailBody.Controls.Add(subjectLabel);
        Panel emailCard = FormFields.CreateCard("Reminder email (to the father, or the mother if needed)", emailBody);
        emailCard.Dock = DockStyle.Fill;
        emailCard.Margin = new Padding(8, 0, 0, 0);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        layout.Controls.Add(studentsCard, 0, 0);
        layout.Controls.Add(emailCard, 1, 0);
        Controls.Add(layout);

        // Attached last so they do not fire while the screen is still being built.
        _searchBox.TextChanged += (_, _) => ShowRows();
        _subjectBox.TextChanged += (_, _) => ShowPreview();
        _bodyBox.TextChanged += (_, _) => ShowPreview();
        Load += (_, _) =>
        {
            LoadText();
            LoadDues();
        };
    }

    private void AddReadOnlyColumn(string property, string header, int weight, string? format = null) =>
        FormFields.AddGridColumn(_grid, property, header, weight, format: format,
            alignRight: format == "C0").ReadOnly = true;

    private ReminderRow? CurrentRow => _grid.CurrentRow?.DataBoundItem as ReminderRow;

    private List<ReminderRow> TickedRows => _rows.Where(r => r.Send).ToList();

    private EmailTemplate CurrentTemplate => new(_subjectBox.Text, _bodyBox.Text);

    private void LoadText()
    {
        try
        {
            EmailTemplate saved = EmailSettingsService.Get().Reminder;
            _subjectBox.Text = saved.Subject;
            _bodyBox.Text = saved.Body.ReplaceLineEndings(Environment.NewLine);
            _textMessage.Text = string.Empty;
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The reminder text could not be loaded.");
        }
    }

    private void SaveText()
    {
        try
        {
            EmailSettingsService.SaveReminderText(CurrentTemplate);
            FormFields.ShowSuccess(_textMessage, "Saved as the default text.");
        }
        catch (ValidationException ex)
        {
            FormFields.ShowError(_textMessage, ex.Message);
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The reminder text could not be saved.");
        }
    }

    private void LoadDues(int? selectStudentId = null)
    {
        try
        {
            HashSet<int> ticked = _rows.Where(r => r.Send).Select(r => r.Due.StudentId).ToHashSet();
            _rows = PendingDuesService.GetDues(_hostel.HostelId)
                .Select(d => new ReminderRow(d) { Send = ticked.Contains(d.StudentId) })
                .ToList();
            ShowRows(selectStudentId);
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The pending fees could not be loaded.");
        }
    }

    private void ShowRows(int? selectStudentId = null)
    {
        string search = _searchBox.Text.Trim();
        List<ReminderRow> shown = search.Length == 0
            ? _rows
            : _rows.Where(r =>
                r.StudentName.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
                r.RoomNumber.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                r.Due.FatherText.Contains(search, StringComparison.CurrentCultureIgnoreCase)).ToList();

        _grid.DataSource = shown;
        if (selectStudentId is not null)
        {
            int index = shown.FindIndex(r => r.Due.StudentId == selectStudentId);
            if (index >= 0)
            {
                _grid.CurrentCell = _grid.Rows[index].Cells[1];
            }
        }

        _summaryLabel.Text = $"{_rows.Count} students, pending {Money.Format(_rows.Sum(r => r.PendingAmount))}";
        UpdateButtons();
        ShowPreview();
    }

    private void SetAllTicks(bool send)
    {
        _grid.EndEdit();
        foreach (ReminderRow row in (_grid.DataSource as List<ReminderRow>) ?? [])
        {
            row.Send = send && row.CanEmail;
        }
        _grid.Refresh();
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        int count = TickedRows.Count;
        _sendButton.Enabled = count > 0;
        _sendButton.Text = count == 0 ? "Send Reminder" : $"Send Reminder ({count})";
        _payButton.Enabled = CurrentRow is not null;
    }

    /// <summary>Shows the email as the current student's parent will receive it.</summary>
    private void ShowPreview()
    {
        // The grid also reports a change while the screen is being closed.
        if (IsDisposed || Disposing || _previewBox.IsDisposed)
        {
            return;
        }

        if (CurrentRow is not ReminderRow row)
        {
            _previewTitle.Text = "Preview";
            _previewBox.Text = _rows.Count == 0 ? "No student has a pending fee." : "Select a student to see the email.";
            return;
        }

        _previewTitle.Text = $"Preview for {row.StudentName}";
        try
        {
            (string to, string subject, string body) = EmailService.PreviewReminder(row.Due, CurrentTemplate);
            _previewBox.Text = $"To: {to}{Environment.NewLine}Subject: {subject}{Environment.NewLine}{Environment.NewLine}" +
                               body.ReplaceLineEndings(Environment.NewLine);
        }
        catch (ValidationException ex)
        {
            _previewBox.Text = ex.Message;
        }
        catch (Exception ex)
        {
            AppLogger.Error("The reminder preview could not be shown.", ex);
            _previewBox.Text = "The preview could not be shown.";
        }
    }

    /// <summary>Emails the reminder, with the text as shown, to the parents of the ticked students.</summary>
    private async Task SendReminders()
    {
        List<ReminderRow> ticked = TickedRows;
        if (ticked.Count == 0)
        {
            return;
        }

        try
        {
            EmailTemplate template = CurrentTemplate;
            EmailService.ValidateReminderText(template);
            (List<OutgoingEmail> emails, List<string> problems) = EmailService.PrepareReminders(ticked.Select(r => r.Due), template);
            string skipped = problems.Count > 0
                ? $"{Environment.NewLine}{problems.Count} student(s) cannot be emailed and will be listed afterwards."
                : "";
            if (emails.Count > 0 &&
                !Dialogs.Confirm($"Send a fee reminder to the parents of {emails.Count} student(s)?{skipped}"))
            {
                return;
            }
            if (emails.Count == 0)
            {
                Dialogs.Warning(string.Join(Environment.NewLine, problems));
                return;
            }

            await EmailSending.SendAsync(this, emails, problems);
            foreach (ReminderRow row in _rows)
            {
                row.Send = false;
            }
            LoadDues(CurrentRow?.Due.StudentId);
        }
        catch (ValidationException ex)
        {
            Dialogs.Warning(ex.Message);
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The reminders could not be sent.");
        }
    }

    /// <summary>Records a payment against the current student's oldest invoice with a pending amount.</summary>
    private void RecordPayment()
    {
        if (CurrentRow is not ReminderRow row)
        {
            return;
        }

        try
        {
            if (PaymentDialog.Show(this, _hostel, row.Due.Invoices[0].InvoiceId) is not null)
            {
                LoadDues(row.Due.StudentId);
            }
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The payment could not be recorded.");
        }
    }

    /// <summary>Saves the students shown as a PDF and opens it.</summary>
    private void ExportPdf()
    {
        using var dialog = new SaveFileDialog
        {
            Title = "Export pending fees as PDF",
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
            List<StudentDue> shown = ((_grid.DataSource as List<ReminderRow>) ?? []).Select(r => r.Due).ToList();
            PendingDuesPdfWriter.Write(_hostel, shown, DateTime.Today, dialog.FileName);
            Process.Start(new ProcessStartInfo(dialog.FileName) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The pending fees PDF could not be saved or opened.");
        }
    }

    /// <summary>One student in the list, with the tick box.</summary>
    private sealed class ReminderRow(StudentDue due)
    {
        public StudentDue Due { get; } = due;
        public bool Send { get; set; }
        public string StudentName => Due.StudentStatus == StudentStatus.Left ? $"{Due.StudentName} (left)" : Due.StudentName;
        public string RoomNumber => Due.RoomNumber;
        public string Years => Due.YearsText;
        public decimal TotalAmount => Due.TotalAmount;
        public decimal PaidAmount => Due.PaidAmount;
        public decimal PendingAmount => Due.PendingAmount;
        public string EmailText => Due.EmailText;
        public DateTime? LastReminderDate => Due.LastReminderDate;
        public bool CanEmail => Due.Contact is not null;
    }
}
