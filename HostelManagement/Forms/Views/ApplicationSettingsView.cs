using HostelManagement.Data;
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
        AddInfoRow(infoTable, "Data folder", AppPaths.DataFolder);
        AddInfoRow(infoTable, "Database file", AppPaths.DatabaseFile);
        AddInfoRow(infoTable, "Database engine", Db.ProviderName);
        _fileSizeValue = AddInfoRow(infoTable, "File size", string.Empty);

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
            Text = "Checks that the application can open the database and add, read, update and delete a record. " +
                   "No data is changed.",
        };

        var buttonRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 50,
            Padding = new Padding(0, 8, 0, 0),
            WrapContents = false,
        };
        buttonRow.Controls.Add(_testButton);
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

        // Docked controls are laid out in reverse order of adding: the grid fills what is left.
        Controls.Add(_resultsGrid);
        Controls.Add(buttonRow);
        Controls.Add(infoTable);
        Controls.Add(heading);

        ShowFileSize();
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
                    step.Passed ? Color.FromArgb(25, 135, 84) : UiTheme.Danger;
            }

            bool allPassed = steps.Count > 0 && steps.All(step => step.Passed);
            _summaryLabel.ForeColor = allPassed ? Color.FromArgb(25, 135, 84) : UiTheme.Danger;
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
}
