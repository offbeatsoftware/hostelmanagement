using HostelManagement.Models;
using HostelManagement.Services;
using HostelManagement.Utilities;

namespace HostelManagement.Forms.Views;

/// <summary>
/// Email Settings: the Gmail account that sends the emails, the editable invoice, receipt and reminder
/// emails, and the history of emails sent.
/// </summary>
public sealed class EmailSettingsView : UserControl
{
    private readonly TextBox _senderEmailBox;
    private readonly TextBox _senderNameBox;
    private readonly TextBox _passwordBox;
    private readonly TextBox _hostBox;
    private readonly NumericUpDown _portBox;
    private readonly Dictionary<string, (TextBox Subject, TextBox Body)> _templates = [];
    private readonly DataGridView _historyGrid;
    private readonly Label _messageLabel;
    private readonly TextBox _testToBox;

    public EmailSettingsView()
    {
        Dock = DockStyle.Fill;
        BackColor = UiTheme.ContentBackground;

        var tabs = new TabControl { Dock = DockStyle.Fill, Font = UiTheme.BodyFont, Padding = new Point(14, 6) };

        // ---- Gmail account ----
        TableLayoutPanel fields = FormFields.CreateTable(labelWidth: 150, inputWidth: 380);
        _senderEmailBox = FormFields.AddTextBox(fields, "Gmail address", 150, required: true);
        _senderNameBox = FormFields.AddTextBox(fields, "Sender name", 100);
        _senderNameBox.PlaceholderText = AppInfo.BusinessName;
        _passwordBox = FormFields.AddTextBox(fields, "App password", 100, required: true);
        _passwordBox.UseSystemPasswordChar = true;
        _hostBox = FormFields.AddTextBox(fields, "SMTP server", 100, required: true);
        _portBox = new NumericUpDown { Minimum = 1, Maximum = 65535, Width = 100, Margin = new Padding(0, 4, 0, 4), TabIndex = fields.Controls.Count };
        FormFields.AddRow(fields, "Port", _portBox, required: true);
        fields.Location = new Point(16, 16);

        var help = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(560, 0),
            ForeColor = UiTheme.TextMuted,
            Location = new Point(16, 215),
            Text = "Gmail needs an app password, not the normal password:\n" +
                   "1. Sign in to the Gmail account and turn on 2-Step Verification (Google Account > Security).\n" +
                   "2. Open myaccount.google.com/apppasswords, create an app password named \"Hostel\".\n" +
                   "3. Copy the 16 letters here and click Save, then Send Test Email.\n\n" +
                   "The password is stored encrypted for this Windows user. After moving the database to another " +
                   "computer or Windows user, enter it again.",
        };

        var testToLabel = new Label { Text = "Send test to", AutoSize = true, Location = new Point(16, 381) };
        _testToBox = new TextBox
        {
            Location = new Point(166, 377),
            Width = 300,
            MaxLength = 150,
            PlaceholderText = "Empty: the Gmail address above",
        };
        var testButton = new Button { Text = "Send Test Email", Location = new Point(476, 375) };
        UiTheme.StyleSecondaryButton(testButton);
        testButton.Width = 150;
        testButton.Click += async (_, _) => await SendTestEmail(testButton);

        var accountPage = new TabPage("Gmail account") { BackColor = Color.White, AutoScroll = true };
        accountPage.Controls.AddRange([fields, help, testToLabel, _testToBox, testButton]);
        tabs.TabPages.Add(accountPage);

        // ---- Email texts ----
        tabs.TabPages.Add(CreateTemplatePage("Invoice email", EmailType.Invoice, EmailSettings.DefaultInvoice));
        tabs.TabPages.Add(CreateTemplatePage("Receipt email", EmailType.Receipt, EmailSettings.DefaultReceipt));
        tabs.TabPages.Add(CreateTemplatePage("Fee reminder email", EmailType.DueReminder, EmailSettings.DefaultReminder));
        tabs.TabPages.Add(CreateTemplatePage("Attendance email", EmailType.Absence, EmailSettings.DefaultAbsence));

        // ---- History ----
        _historyGrid = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false };
        UiTheme.StyleGrid(_historyGrid);
        FormFields.AddGridColumn(_historyGrid, nameof(EmailHistoryEntry.SentDate), "Date", 12, format: "dd MMM yyyy HH:mm");
        FormFields.AddGridColumn(_historyGrid, nameof(EmailHistoryEntry.TypeText), "Email", 8);
        FormFields.AddGridColumn(_historyGrid, nameof(EmailHistoryEntry.StudentName), "Student", 14);
        FormFields.AddGridColumn(_historyGrid, nameof(EmailHistoryEntry.InvoiceNumber), "Invoice no.", 12);
        FormFields.AddGridColumn(_historyGrid, nameof(EmailHistoryEntry.RecipientEmail), "Sent to", 17);
        FormFields.AddGridColumn(_historyGrid, nameof(EmailHistoryEntry.Status), "Status", 7);
        FormFields.AddGridColumn(_historyGrid, nameof(EmailHistoryEntry.ErrorMessage), "Problem", 30);
        _historyGrid.CellFormatting += (_, e) =>
        {
            if (e.RowIndex >= 0 && _historyGrid.Rows[e.RowIndex].DataBoundItem is EmailHistoryEntry { Status: EmailStatus.Failed } &&
                e.CellStyle is not null)
            {
                e.CellStyle.ForeColor = UiTheme.Danger;
            }
        };
        var refreshButton = new Button { Text = "Refresh" };
        UiTheme.StyleSecondaryButton(refreshButton);
        refreshButton.Click += (_, _) => LoadHistory();
        var historyNote = FormFields.CreateMessageLabel();
        historyNote.ForeColor = UiTheme.TextMuted;
        historyNote.Text = "The latest 500 emails of all hostels. Fee reminders list one row per invoice.";
        FlowLayoutPanel historyToolbar = FormFields.CreateButtonRow(refreshButton, historyNote);
        historyToolbar.Dock = DockStyle.Top;
        var historyPage = new TabPage("Email history") { BackColor = Color.White, Padding = new Padding(8) };
        historyPage.Controls.Add(_historyGrid);
        historyPage.Controls.Add(historyToolbar);
        tabs.TabPages.Add(historyPage);
        tabs.SelectedIndexChanged += (_, _) =>
        {
            if (tabs.SelectedTab == historyPage)
            {
                LoadHistory();
            }
        };

        // ---- Save ----
        var saveButton = new Button { Text = "Save" };
        UiTheme.StylePrimaryButton(saveButton);
        saveButton.Click += (_, _) => Save();
        _messageLabel = FormFields.CreateMessageLabel();
        FlowLayoutPanel saveRow = FormFields.CreateButtonRow(saveButton, _messageLabel);
        saveRow.Dock = DockStyle.Bottom;

        var body = new Panel { Dock = DockStyle.Fill };
        body.Controls.Add(tabs);
        body.Controls.Add(saveRow);
        Panel card = FormFields.CreateCard("Email settings (one Gmail account for all hostels)", body);
        card.Dock = DockStyle.Fill;
        Controls.Add(card);

        Load += (_, _) => LoadSettings();
    }

    private TabPage CreateTemplatePage(string title, string type, EmailTemplate defaults)
    {
        var subject = new TextBox { Font = UiTheme.BodyFont, MaxLength = 255, Dock = DockStyle.Top };
        var bodyBox = new TextBox
        {
            Font = UiTheme.BodyFont,
            Multiline = true,
            AcceptsReturn = true,
            ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Fill,
        };
        _templates[type] = (subject, bodyBox);

        var fieldsLabel = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 48,
            ForeColor = UiTheme.TextMuted,
            Padding = new Padding(0, 6, 0, 0),
            Text = "Fields filled in when the email is sent: " +
                   string.Join("  ", EmailSettingsService.Fields[type].Select(f => "{" + f + "}")),
        };
        var restoreButton = new Button { Text = "Restore Default" };
        UiTheme.StyleSecondaryButton(restoreButton);
        restoreButton.Width = 150;
        restoreButton.Click += (_, _) =>
        {
            subject.Text = defaults.Subject;
            bodyBox.Text = defaults.Body.ReplaceLineEndings("\r\n");
        };
        var restoreRow = FormFields.CreateButtonRow(restoreButton);
        restoreRow.Dock = DockStyle.Bottom;

        var subjectLabel = new Label { Text = "Subject", Dock = DockStyle.Top, Height = 22, Font = UiTheme.BodyBoldFont };
        var messageLabel = new Label { Text = "Message", Dock = DockStyle.Top, Height = 26, Font = UiTheme.BodyBoldFont, Padding = new Padding(0, 6, 0, 0) };

        var page = new TabPage(title) { BackColor = Color.White, Padding = new Padding(12) };
        // Docked controls are laid out in reverse order of adding: the message box fills what is left.
        page.Controls.Add(bodyBox);
        page.Controls.Add(messageLabel);
        page.Controls.Add(subject);
        page.Controls.Add(subjectLabel);
        page.Controls.Add(fieldsLabel);
        page.Controls.Add(restoreRow);
        return page;
    }

    private void LoadSettings()
    {
        try
        {
            EmailSettings settings = EmailSettingsService.Get();
            _senderEmailBox.Text = settings.SenderEmail;
            _senderNameBox.Text = settings.SenderName;
            _passwordBox.Text = settings.AppPassword;
            _hostBox.Text = settings.SmtpHost;
            _portBox.Value = settings.SmtpPort;
            ShowTemplate(EmailType.Invoice, settings.Invoice);
            ShowTemplate(EmailType.Receipt, settings.Receipt);
            ShowTemplate(EmailType.DueReminder, settings.Reminder);
            ShowTemplate(EmailType.Absence, settings.Absence);
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The email settings could not be loaded.");
        }
    }

    private void ShowTemplate(string type, EmailTemplate template)
    {
        _templates[type].Subject.Text = template.Subject;
        _templates[type].Body.Text = template.Body.ReplaceLineEndings("\r\n");
    }

    private EmailTemplate ReadTemplate(string type) => new(_templates[type].Subject.Text, _templates[type].Body.Text);

    private EmailSettings ReadSettings() => new()
    {
        SenderEmail = _senderEmailBox.Text,
        SenderName = _senderNameBox.Text,
        AppPassword = _passwordBox.Text,
        SmtpHost = _hostBox.Text,
        SmtpPort = (int)_portBox.Value,
        Invoice = ReadTemplate(EmailType.Invoice),
        Receipt = ReadTemplate(EmailType.Receipt),
        Reminder = ReadTemplate(EmailType.DueReminder),
        Absence = ReadTemplate(EmailType.Absence),
    };

    private bool Save()
    {
        _messageLabel.Text = string.Empty;
        try
        {
            EmailSettingsService.Save(ReadSettings());
            FormFields.ShowSuccess(_messageLabel, "Saved.");
            return true;
        }
        catch (ValidationException ex)
        {
            FormFields.ShowError(_messageLabel, ex.Message);
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The email settings could not be saved.");
        }
        return false;
    }

    /// <summary>Saves, then sends a test email to the address entered (or to the Gmail address itself).</summary>
    private async Task SendTestEmail(Button button)
    {
        if (!Save())
        {
            return;
        }

        EmailSettings settings = EmailSettingsService.Get();
        button.Enabled = false;
        Cursor = Cursors.WaitCursor;
        try
        {
            string to = _testToBox.Text.Trim();
            string? error = await Task.Run(() => EmailService.SendTest(settings, to));
            if (error is null)
            {
                FormFields.ShowSuccess(_messageLabel, $"Test email sent to {(to.Length > 0 ? to : settings.SenderEmail)}. Check its inbox.");
            }
            else
            {
                FormFields.ShowError(_messageLabel, error);
            }
        }
        finally
        {
            button.Enabled = true;
            Cursor = Cursors.Default;
        }
    }

    private void LoadHistory()
    {
        try
        {
            _historyGrid.DataSource = EmailService.GetHistory();
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The email history could not be loaded.");
        }
    }
}
