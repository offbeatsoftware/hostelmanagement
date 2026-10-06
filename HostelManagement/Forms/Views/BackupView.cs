using System.Diagnostics;
using HostelManagement.Models;
using HostelManagement.Services;
using HostelManagement.Utilities;

namespace HostelManagement.Forms.Views;

/// <summary>
/// Backup / Restore: make a backup now, choose a second backup folder, see the backups and restore one.
/// </summary>
public sealed class BackupView : UserControl
{
    private readonly TextBox _secondFolderBox;
    private readonly DataGridView _grid;
    private readonly Label _messageLabel;
    private readonly Button _restoreButton;

    public BackupView()
    {
        Dock = DockStyle.Fill;
        BackColor = UiTheme.ContentBackground;

        var intro = new Label
        {
            Dock = DockStyle.Top,
            Height = 58,
            ForeColor = UiTheme.TextMuted,
            Text = "A backup is one zip file with the database and all student photos and Aadhaar cards. " +
                   "An automatic backup is made once a day when the application is closed; the last " +
                   $"{BackupService.AutomaticBackupsToKeep} automatic backups are kept. Backups made with Backup Now are never deleted.",
        };

        var backupButton = new Button { Text = "Backup Now" };
        UiTheme.StylePrimaryButton(backupButton);
        backupButton.Width = 130;
        backupButton.Click += (_, _) => BackupNow();

        var openFolderButton = new Button { Text = "Open Backups Folder" };
        UiTheme.StyleSecondaryButton(openFolderButton);
        openFolderButton.Width = 170;
        openFolderButton.Click += (_, _) => OpenFolder(AppPaths.BackupsFolder);

        _messageLabel = FormFields.CreateMessageLabel();
        FlowLayoutPanel backupRow = FormFields.CreateButtonRow(backupButton, openFolderButton, _messageLabel);
        backupRow.Dock = DockStyle.Top;

        // ---- Second copy ----
        var secondLabel = new Label { Text = "Second copy in", AutoSize = true, Margin = new Padding(0, 10, 8, 0) };
        _secondFolderBox = new TextBox
        {
            ReadOnly = true,
            Width = 380,
            Margin = new Padding(0, 6, 8, 0),
            PlaceholderText = "Not set: choose a USB drive or a Google Drive / OneDrive folder",
        };
        var chooseButton = new Button { Text = "Choose..." };
        UiTheme.StyleSecondaryButton(chooseButton);
        chooseButton.Click += (_, _) => ChooseSecondFolder();
        var clearButton = new Button { Text = "Clear" };
        UiTheme.StyleSecondaryButton(clearButton);
        clearButton.Click += (_, _) => SaveSecondFolder(string.Empty);
        FlowLayoutPanel secondRow = FormFields.CreateButtonRow(secondLabel, _secondFolderBox, chooseButton, clearButton);
        secondRow.Dock = DockStyle.Top;

        var backupBody = new Panel { Dock = DockStyle.Fill };
        backupBody.Controls.Add(secondRow);
        backupBody.Controls.Add(backupRow);
        backupBody.Controls.Add(intro);
        Panel backupCard = FormFields.CreateCard("Backup", backupBody);
        backupCard.Dock = DockStyle.Top;
        backupCard.Height = 200;

        // ---- Backups and restore ----
        _grid = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false };
        UiTheme.StyleGrid(_grid);
        FormFields.AddGridColumn(_grid, nameof(BackupFile.Created), "Made on", 18, format: "dd MMM yyyy HH:mm");
        FormFields.AddGridColumn(_grid, nameof(BackupFile.KindText), "Kind", 16);
        FormFields.AddGridColumn(_grid, nameof(BackupFile.SizeText), "Size", 10, alignRight: true);
        FormFields.AddGridColumn(_grid, nameof(BackupFile.FileName), "File", 46);
        _grid.SelectionChanged += (_, _) => _restoreButton!.Enabled = _grid.CurrentRow?.DataBoundItem is BackupFile;

        _restoreButton = new Button { Text = "Restore Selected..." };
        UiTheme.StyleDangerButton(_restoreButton);
        _restoreButton.Width = 170;
        _restoreButton.Click += (_, _) =>
        {
            if (_grid.CurrentRow?.DataBoundItem is BackupFile backup)
            {
                RestoreFrom(backup.FilePath);
            }
        };
        var restoreFileButton = new Button { Text = "Restore From File..." };
        UiTheme.StyleSecondaryButton(restoreFileButton);
        restoreFileButton.Width = 170;
        restoreFileButton.Click += (_, _) => RestoreFromChosenFile();
        FlowLayoutPanel restoreRow = FormFields.CreateButtonRow(_restoreButton, restoreFileButton);
        restoreRow.Dock = DockStyle.Top;

        var listBody = new Panel { Dock = DockStyle.Fill };
        listBody.Controls.Add(_grid);
        listBody.Controls.Add(restoreRow);
        Panel listCard = FormFields.CreateCard("Backups and restore", listBody);
        listCard.Dock = DockStyle.Fill;

        var gap = new Panel { Dock = DockStyle.Top, Height = 10 };
        Controls.Add(listCard);
        Controls.Add(gap);
        Controls.Add(backupCard);

        Load += (_, _) =>
        {
            LoadBackups();
            LoadSecondFolder();
        };
    }

    private void LoadBackups(string? selectPath = null)
    {
        try
        {
            List<BackupFile> backups = BackupService.GetBackups();
            _grid.DataSource = backups;
            int index = selectPath is null ? -1 : backups.FindIndex(b => b.FilePath == selectPath);
            if (index >= 0)
            {
                _grid.CurrentCell = _grid.Rows[index].Cells[0];
            }
            _restoreButton.Enabled = _grid.CurrentRow?.DataBoundItem is BackupFile;
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The backups could not be listed.");
        }
    }

    private void LoadSecondFolder()
    {
        try
        {
            _secondFolderBox.Text = BackupService.GetSecondFolder();
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The backup settings could not be loaded.");
        }
    }

    private void BackupNow()
    {
        _messageLabel.Text = string.Empty;
        Cursor = Cursors.WaitCursor;
        try
        {
            BackupResult result = BackupService.Create(BackupKind.Manual, DateTime.Now);
            LoadBackups(result.File.FilePath);
            if (result.SecondCopyProblem is string problem)
            {
                FormFields.ShowError(_messageLabel, problem);
            }
            else
            {
                FormFields.ShowSuccess(_messageLabel, $"Backup saved ({result.File.SizeText})" +
                    (BackupService.GetSecondFolder().Length > 0 ? ", with a second copy." : "."));
            }
        }
        catch (IOException ex)
        {
            ErrorHandler.Handle(ex, "The backup could not be saved. Check that the disk is not full.");
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The backup could not be made.");
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private void ChooseSecondFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Choose where a second copy of each backup is saved (a USB drive, or a Google Drive or OneDrive folder)",
            UseDescriptionForTitle = true,
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            SaveSecondFolder(dialog.SelectedPath);
        }
    }

    private void SaveSecondFolder(string folder)
    {
        _messageLabel.Text = string.Empty;
        try
        {
            BackupService.SetSecondFolder(folder);
            _secondFolderBox.Text = folder;
            FormFields.ShowSuccess(_messageLabel, folder.Length > 0 ? "Second copy folder saved." : "Second copy turned off.");
        }
        catch (ValidationException ex)
        {
            FormFields.ShowError(_messageLabel, ex.Message);
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The second backup folder could not be saved.");
        }
    }

    private void RestoreFromChosenFile()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Choose a backup to restore",
            Filter = "Hostel backup (*.zip)|*.zip",
            InitialDirectory = BackupService.GetSecondFolder() is { Length: > 0 } second && Directory.Exists(second) ? second : AppPaths.BackupsFolder,
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            RestoreFrom(dialog.FileName);
        }
    }

    /// <summary>Checks the backup, asks for confirmation, restores it and restarts the application.</summary>
    private void RestoreFrom(string zipPath)
    {
        try
        {
            BackupDetails details = BackupService.Inspect(zipPath);
            if (!Dialogs.Confirm(
                    $"Restore the backup made on {details.Created:dd MMM yyyy} at {details.Created:HH:mm}?\n\n" +
                    "ALL current data (hostels, students, rooms, invoices, payments, photos and documents) will be replaced " +
                    "by the data in this backup. A backup of the current data is saved first, so this can be undone.\n\n" +
                    "The application restarts afterwards."))
            {
                return;
            }

            Cursor = Cursors.WaitCursor;
            BackupResult safety = BackupService.Restore(zipPath, DateTime.Now);
            BackupService.SkipAutomaticBackupOnExit = true;
            Dialogs.Info($"The backup was restored. The previous data was saved as {safety.File.FileName}.\n\n" +
                         "The application will now restart.");
            Application.Restart();
        }
        catch (ValidationException ex)
        {
            Dialogs.Warning(ex.Message);
        }
        catch (IOException ex)
        {
            ErrorHandler.Handle(ex, "The backup could not be restored because a file is in use. " +
                                    "Close Microsoft Access and any open photos or documents, then try again.");
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The backup could not be restored.");
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private static void OpenFolder(string folder)
    {
        try
        {
            Directory.CreateDirectory(folder);
            Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The folder could not be opened.");
        }
    }
}
