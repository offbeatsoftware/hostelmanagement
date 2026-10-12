using HostelManagement.Data;
using HostelManagement.Models;
using HostelManagement.Services;
using HostelManagement.Utilities;

namespace HostelManagement.Forms.Views;

/// <summary>
/// Application Settings screen. Currently shows the database location and a
/// database check that verifies open, insert, read, update and delete.
/// </summary>
public sealed class ApplicationSettingsView : UserControl
{
    private readonly Label _fileSizeValue;
    private readonly Button _testButton;
    private readonly DataGridView _resultsGrid;
    private readonly Label _summaryLabel;
    private readonly ComboBox _startMonthBox;
    private readonly Label _endLabel;
    private readonly Label _currentYearLabel;
    private readonly Label _yearMessage;

    public ApplicationSettingsView()
    {
        Dock = DockStyle.Fill;
        BackColor = UiTheme.ContentBackground;

        var infoTable = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            Padding = new Padding(0, 4, 0, 4),
        };
        infoTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        infoTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddInfoRow(infoTable, "Version", $"{AppInfo.ProductName} {AppInfo.Version}");
        AddInfoRow(infoTable, "Data folder", AppPaths.DataFolder);
        AddInfoRow(infoTable, "Database file", AppPaths.DatabaseFile);
        AddInfoRow(infoTable, "Database engine", Db.ProviderName);
        _fileSizeValue = AddInfoRow(infoTable, "File size", string.Empty);
        // A long folder path wraps within its column (never past the edge) and stays readable in full.
        infoTable.SizeChanged += (_, _) =>
        {
            int width = infoTable.ClientSize.Width - infoTable.Padding.Horizontal - 140;
            foreach (Label value in infoTable.Controls.OfType<Label>().Where(l => infoTable.GetPositionFromControl(l).Column == 1))
            {
                if (width > 100 && value.MaximumSize.Width != width)
                {
                    value.MaximumSize = new Size(width, 0);
                }
            }
        };

        _testButton = new Button { Text = "Test Database", TabIndex = 0 };
        UiTheme.StylePrimaryButton(_testButton);
        _testButton.Width = 140;
        _testButton.Click += (_, _) => RunDatabaseCheck();

        _summaryLabel = new Label
        {
            AutoSize = true,
            Font = UiTheme.BodyFont,
            ForeColor = UiTheme.TextMuted,
            Margin = new Padding(12, 9, 0, 0),
            MaximumSize = new Size(560, 0),
            Text = "Checks that the application can open the database and add, read, update and delete a record. " +
                   "No data is changed.",
        };

        var buttonRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(0, 8, 0, 8),
            WrapContents = true,
        };
        var guideButton = new Button { Text = "User Guide", TabIndex = 1 };
        UiTheme.StyleSecondaryButton(guideButton);
        guideButton.Width = 120;
        guideButton.Margin = new Padding(8, 0, 0, 0);
        guideButton.Click += (_, _) => OpenUserGuide();

        buttonRow.Controls.Add(_testButton);
        buttonRow.Controls.Add(guideButton);
        buttonRow.Controls.Add(_summaryLabel);

        _resultsGrid = new DataGridView { Dock = DockStyle.Fill, TabIndex = 1 };
        UiTheme.StyleGrid(_resultsGrid);
        _resultsGrid.Columns.Add("Step", "Step");
        _resultsGrid.Columns.Add("Result", "Result");
        _resultsGrid.Columns.Add("Details", "Details");
        _resultsGrid.Columns["Step"]!.FillWeight = 25;
        _resultsGrid.Columns["Result"]!.FillWeight = 12;
        _resultsGrid.Columns["Details"]!.FillWeight = 63;

        var heading = new Label
        {
            Dock = DockStyle.Top,
            Height = 28,
            Font = UiTheme.BodyBoldFont,
            ForeColor = UiTheme.TextPrimary,
            Text = "Database",
        };

        // ---- Academic year ----
        var yearHeading = new Label
        {
            Dock = DockStyle.Top,
            Height = 28,
            Font = UiTheme.BodyBoldFont,
            ForeColor = UiTheme.TextPrimary,
            Text = "Academic year",
        };
        TableLayoutPanel yearTable = FormFields.CreateTable(labelWidth: 140, inputWidth: 520);
        yearTable.Dock = DockStyle.Top;
        _startMonthBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = UiTheme.BodyFont,
            Width = 160,
            Margin = new Padding(0, 4, 0, 4),
        };
        _startMonthBox.Items.AddRange([.. Enumerable.Range(1, 12).Select(m => "1 " + AcademicYear.MonthName(m))]);
        _startMonthBox.SelectedIndexChanged += (_, _) => ShowYearPreview();
        FormFields.AddRow(yearTable, "Starts on", _startMonthBox);
        _endLabel = InfoValue();
        FormFields.AddRow(yearTable, "Ends on", _endLabel);
        _currentYearLabel = InfoValue();
        FormFields.AddRow(yearTable, "Current year", _currentYearLabel);

        var saveYearButton = new Button { Text = "Save" };
        UiTheme.StylePrimaryButton(saveYearButton);
        saveYearButton.Click += (_, _) => SaveStartMonth();
        _yearMessage = FormFields.CreateMessageLabel();
        var yearNote = new Label
        {
            AutoSize = true,
            ForeColor = UiTheme.TextMuted,
            Margin = new Padding(12, 9, 0, 0),
            Text = "Fees, invoice numbers, the dashboard, reports and I-cards follow this year.",
        };
        FlowLayoutPanel yearButtons = FormFields.CreateButtonRow(saveYearButton, _yearMessage, yearNote);
        FormFields.AddRow(yearTable, string.Empty, yearButtons);
        var yearGap = new Panel { Dock = DockStyle.Top, Height = 16 };

        // Docked controls are laid out in reverse order of adding: the grid fills what is left.
        Controls.Add(_resultsGrid);
        Controls.Add(buttonRow);
        Controls.Add(infoTable);
        Controls.Add(heading);
        Controls.Add(yearGap);
        Controls.Add(yearTable);
        Controls.Add(yearHeading);

        _startMonthBox.SelectedIndex = AcademicYear.StartMonth - 1;

        ShowFileSize();
    }

    private static Label InfoValue() => new()
    {
        AutoSize = true,
        Font = UiTheme.BodyFont,
        ForeColor = UiTheme.TextPrimary,
        Margin = new Padding(0, 8, 0, 4),
    };

    private int ChosenStartMonth => _startMonthBox.SelectedIndex + 1;

    /// <summary>Shows when a year with the chosen start month ends, and which year today falls in.</summary>
    private void ShowYearPreview()
    {
        int month = ChosenStartMonth;
        _endLabel.Text = AcademicYear.EndText(month);
        int startYear = DateTime.Today.Month >= month ? DateTime.Today.Year : DateTime.Today.Year - 1;
        DateTime start = new(startYear, month, 1);
        string label = month == 1 ? $"{startYear}" : $"{startYear}-{(startYear + 1) % 100:00}";
        _currentYearLabel.Text = string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"{label}: {start:d MMM yyyy} to {start.AddYears(1).AddDays(-1):d MMM yyyy}");
        _yearMessage.Text = string.Empty;
    }

    private void SaveStartMonth()
    {
        int month = ChosenStartMonth;
        if (month == AcademicYear.StartMonth)
        {
            FormFields.ShowSuccess(_yearMessage, "No change.");
            return;
        }
        if (!Dialogs.Confirm(
                $"Start the academic year on 1 {AcademicYear.MonthName(month)} from now on?\n\n" +
                "New fees, invoice numbers, the dashboard, reports and I-cards will use the new year. " +
                "Fees already made keep their academic year."))
        {
            return;
        }
        try
        {
            AcademicYearService.SaveStartMonth(month);
            FormFields.ShowSuccess(_yearMessage, "Saved.");
        }
        catch (ValidationException ex)
        {
            FormFields.ShowError(_yearMessage, ex.Message);
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The academic year could not be saved.");
        }
    }

    private static Label AddInfoRow(TableLayoutPanel table, string caption, string value)
    {
        var captionLabel = new Label
        {
            AutoSize = true,
            Font = UiTheme.BodyFont,
            ForeColor = UiTheme.TextMuted,
            Text = caption,
            Margin = new Padding(0, 4, 0, 4),
        };
        var valueLabel = new Label
        {
            AutoSize = true,
            Font = UiTheme.BodyFont,
            ForeColor = UiTheme.TextPrimary,
            Text = value,
            Margin = new Padding(0, 4, 0, 4),
        };
        table.Controls.Add(captionLabel);
        table.Controls.Add(valueLabel);
        return valueLabel;
    }

    private void ShowFileSize()
    {
        try
        {
            var file = new FileInfo(AppPaths.DatabaseFile);
            _fileSizeValue.Text = file.Exists ? $"{file.Length / 1024.0:N0} KB" : "File not found";
        }
        catch (IOException)
        {
            _fileSizeValue.Text = "Unknown";
        }
    }

    private void RunDatabaseCheck()
    {
        _testButton.Enabled = false;
        Cursor = Cursors.WaitCursor;
        _resultsGrid.Rows.Clear();
        try
        {
            List<DatabaseCheckStep> steps = DatabaseCheckService.Run();
            foreach (DatabaseCheckStep step in steps)
            {
                int index = _resultsGrid.Rows.Add(step.Step, step.Passed ? "Passed" : "Failed", step.Details);
                _resultsGrid.Rows[index].Cells["Result"].Style.ForeColor =
                    step.Passed ? UiTheme.Success : UiTheme.Danger;
            }

            bool allPassed = steps.Count > 0 && steps.All(step => step.Passed);
            _summaryLabel.ForeColor = allPassed ? UiTheme.Success : UiTheme.Danger;
            _summaryLabel.Text = allPassed
                ? $"All checks passed ({DateTime.Now:HH:mm:ss})."
                : "One or more checks failed. See the details below.";
            ShowFileSize();
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The database check could not be completed.");
        }
        finally
        {
            Cursor = Cursors.Default;
            _testButton.Enabled = true;
        }
    }

    /// <summary>Opens the user guide (UserGuide.pdf next to the program).</summary>
    private static void OpenUserGuide()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "UserGuide.pdf");
        if (!File.Exists(path))
        {
            Dialogs.Warning($"The user guide was not found ({path}). Reinstall the application to restore it.");
            return;
        }
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The user guide could not be opened. Check that a PDF viewer is installed.");
        }
    }
}
