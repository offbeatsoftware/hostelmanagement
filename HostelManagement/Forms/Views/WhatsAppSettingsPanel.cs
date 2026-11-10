using HostelManagement.Models;
using HostelManagement.Services;
using HostelManagement.Utilities;

namespace HostelManagement.Forms.Views;

/// <summary>
/// The WhatsApp tab of Email Settings: the hostel's WhatsApp Business account on Meta (Phone Number ID, Business
/// Account ID, access token), the names of the two approved templates, the payment QR image and a test message.
/// Saved on its own, so WhatsApp can be set up before or without Gmail.
/// </summary>
public sealed class WhatsAppSettingsPanel : UserControl
{
    private readonly TextBox _phoneNumberIdBox;
    private readonly TextBox _accountIdBox;
    private readonly TextBox _tokenBox;
    private readonly TextBox _feeTemplateBox;
    private readonly TextBox _attendanceTemplateBox;
    private readonly TextBox _languageBox;
    private readonly Label _qrLabel;
    private readonly Button _removeQrButton;
    private readonly TextBox _testToBox;
    private readonly Label _messageLabel;
    private string? _newQrFile;
    private bool _removeQr;

    public WhatsAppSettingsPanel()
    {
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        BackColor = Color.White;

        TableLayoutPanel fields = FormFields.CreateTable(labelWidth: 190, inputWidth: 380);
        _phoneNumberIdBox = FormFields.AddTextBox(fields, "Phone Number ID", 30, required: true);
        _accountIdBox = FormFields.AddTextBox(fields, "WhatsApp Business Account ID", 30);
        _tokenBox = FormFields.AddTextBox(fields, "Access token", 600, required: true);
        _tokenBox.UseSystemPasswordChar = true;
        _feeTemplateBox = FormFields.AddTextBox(fields, "Fee reminder template", 100, required: true);
        _attendanceTemplateBox = FormFields.AddTextBox(fields, "Attendance template", 100, required: true);
        _languageBox = FormFields.AddTextBox(fields, "Template language", 10, required: true);

        _qrLabel = new Label { AutoSize = true, MaximumSize = new Size(380, 0), Margin = new Padding(0, 8, 0, 4) };
        FormFields.AddRow(fields, "Payment QR image", _qrLabel);
        var chooseQrButton = new Button { Text = "Choose QR..." };
        UiTheme.StyleSecondaryButton(chooseQrButton);
        chooseQrButton.Width = 120;
        chooseQrButton.Click += (_, _) => ChooseQr();
        _removeQrButton = new Button { Text = "Remove" };
        UiTheme.StyleSecondaryButton(_removeQrButton);
        _removeQrButton.Click += (_, _) =>
        {
            _newQrFile = null;
            _removeQr = true;
            ShowQr(null);
        };
        FormFields.AddRow(fields, string.Empty, FormFields.CreateButtonRow(chooseQrButton, _removeQrButton));

        var saveButton = new Button { Text = "Save WhatsApp Settings" };
        UiTheme.StylePrimaryButton(saveButton);
        saveButton.Width = 200;
        saveButton.Click += (_, _) => Save();
        _messageLabel = FormFields.CreateMessageLabel();
        _messageLabel.MaximumSize = new Size(560, 0);
        FlowLayoutPanel saveRow = FormFields.CreateButtonRow(saveButton, _messageLabel);

        var testToLabel = new Label { Text = "Send test to mobile", AutoSize = true, Margin = new Padding(0, 10, 8, 0) };
        _testToBox = new TextBox { Margin = new Padding(0, 6, 8, 0), Width = 200, MaxLength = 20, PlaceholderText = "98765 43210" };
        var testButton = new Button { Text = "Send Test Message" };
        UiTheme.StyleSecondaryButton(testButton);
        testButton.Width = 170;
        testButton.Click += async (_, _) => await SendTest(testButton);
        FlowLayoutPanel testRow = FormFields.CreateButtonRow(testToLabel, _testToBox, testButton);

        var help = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(640, 0),
            ForeColor = UiTheme.TextMuted,
            Margin = new Padding(0, 10, 0, 10),
            Text = "From Meta (see the user guide for the full steps):\n" +
                   "• Phone Number ID and WhatsApp Business Account ID: developers.facebook.com > your app > WhatsApp > API Setup.\n" +
                   "• Access token: Business Settings > System users > Generate token (whatsapp_business_messaging). " +
                   "It is stored encrypted for this Windows user and never shown again; leave the box empty to keep it.\n" +
                   "• Templates: the names approved in WhatsApp Manager (fee_reminder, attendance_alert), language en.\n" +
                   "• Payment QR image: only when the fee reminder template has an image header; otherwise remove it.\n" +
                   "• Send Test Message sends Meta's \"hello_world\" message to check the settings.",
        };

        FlowLayoutPanel stack = FormFields.CreateStack(fields, saveRow, help, testRow);
        Controls.Add(stack);

        Load += (_, _) => LoadSettings();
    }

    private void LoadSettings()
    {
        try
        {
            WhatsAppSettings settings = WhatsAppSettingsService.Get();
            _phoneNumberIdBox.Text = settings.PhoneNumberId;
            _accountIdBox.Text = settings.BusinessAccountId;
            _tokenBox.Text = string.Empty;
            _tokenBox.PlaceholderText = settings.AccessToken.Length > 0 ? "Saved (hidden). Type a new token to replace it." : "Starts with EAA";
            _feeTemplateBox.Text = settings.FeeTemplate;
            _attendanceTemplateBox.Text = settings.AttendanceTemplate;
            _languageBox.Text = settings.Language;
            _newQrFile = null;
            _removeQr = false;
            ShowQr(WhatsAppSettingsService.PaymentQrFile(settings));
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The WhatsApp settings could not be loaded.");
        }
    }

    private void ShowQr(string? file)
    {
        _qrLabel.Text = file is null ? "None (the fee reminder is sent without an image)" : Path.GetFileName(file);
        _qrLabel.ForeColor = file is null ? UiTheme.TextMuted : UiTheme.TextPrimary;
        _removeQrButton.Enabled = file is not null;
    }

    private void ChooseQr()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Choose the payment QR code image",
            Filter = "Images (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg",
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _newQrFile = dialog.FileName;
            _removeQr = false;
            ShowQr(dialog.FileName);
        }
    }

    private bool Save()
    {
        _messageLabel.Text = string.Empty;
        try
        {
            WhatsAppSettingsService.Save(new WhatsAppSettings
            {
                PhoneNumberId = _phoneNumberIdBox.Text,
                BusinessAccountId = _accountIdBox.Text,
                AccessToken = _tokenBox.Text,
                FeeTemplate = _feeTemplateBox.Text,
                AttendanceTemplate = _attendanceTemplateBox.Text,
                Language = _languageBox.Text,
            }, _newQrFile, _removeQr);
            LoadSettings();
            FormFields.ShowSuccess(_messageLabel, "Saved.");
            return true;
        }
        catch (ValidationException ex)
        {
            FormFields.ShowError(_messageLabel, ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The WhatsApp settings could not be saved.");
            return false;
        }
    }

    private async Task SendTest(Button button)
    {
        if (!Save())
        {
            return;
        }
        WhatsAppSettings settings = WhatsAppSettingsService.Get();
        string to = _testToBox.Text.Trim();
        button.Enabled = false;
        Cursor = Cursors.WaitCursor;
        try
        {
            string? error = await Task.Run(() => WhatsAppService.SendTest(settings, to));
            if (error is null)
            {
                FormFields.ShowSuccess(_messageLabel, $"Test message sent to {to}. Check WhatsApp on that phone.");
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
}
