using HostelManagement.Models;
using HostelManagement.Services;
using HostelManagement.Utilities;

namespace HostelManagement.Forms.Views;

/// <summary>
/// The WhatsApp tab of Email Settings. Manual mode (default): the two messages typed into each parent's chat in
/// WhatsApp on this PC, and whether chats open in the WhatsApp app or WhatsApp Web. API mode: the hostel's WhatsApp
/// Business Platform account on Meta (Phone Number ID, Business Account ID, access token), the names of the two
/// approved templates and a test message. The payment QR image is used by both. Saved on its own, without Gmail.
/// </summary>
public sealed class WhatsAppSettingsPanel : UserControl
{
    private readonly RadioButton _manualRadio;
    private readonly RadioButton _apiRadio;
    private readonly ComboBox _openInBox;
    private readonly TextBox _feeTextBox;
    private readonly TextBox _attendanceTextBox;
    private readonly FlowLayoutPanel _manualSection;
    private readonly FlowLayoutPanel _apiSection;
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

        // ---- How to send ----
        _manualRadio = new RadioButton
        {
            AutoSize = true,
            Checked = true,
            Margin = new Padding(0, 4, 0, 2),
            Text = "WhatsApp on this PC: the program opens each parent's chat with the message typed; you press Send",
        };
        _apiRadio = new RadioButton
        {
            AutoSize = true,
            Margin = new Padding(0, 2, 0, 8),
            Text = "Meta's WhatsApp API: sent automatically (needs an approved WhatsApp Business Platform account)",
        };
        _manualRadio.CheckedChanged += (_, _) => ShowMode();

        // ---- Manual messages ----
        TableLayoutPanel manualFields = FormFields.CreateTable(labelWidth: 190, inputWidth: 470);
        _openInBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300, Margin = new Padding(0, 4, 0, 4) };
        _openInBox.Items.AddRange(["WhatsApp app on this PC", "WhatsApp Web in the browser"]);
        FormFields.AddRow(manualFields, "Open chats in", _openInBox);
        _feeTextBox = FormFields.AddTextBox(manualFields, "Fee reminder message", 1500, required: true, multiline: true);
        _feeTextBox.Height = 230;
        _attendanceTextBox = FormFields.AddTextBox(manualFields, "Attendance message", 1500, required: true, multiline: true);
        _attendanceTextBox.Height = 90;
        var defaultsButton = new Button { Text = "Restore Default Texts" };
        UiTheme.StyleSecondaryButton(defaultsButton);
        defaultsButton.Width = 190;
        defaultsButton.Click += (_, _) =>
        {
            _feeTextBox.Text = WhatsAppSettings.DefaultFeeText.ReplaceLineEndings(Environment.NewLine);
            _attendanceTextBox.Text = WhatsAppSettings.DefaultAttendanceText.ReplaceLineEndings(Environment.NewLine);
        };
        FormFields.AddRow(manualFields, string.Empty, FormFields.CreateButtonRow(defaultsButton));
        var manualHelp = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(660, 0),
            ForeColor = UiTheme.TextMuted,
            Margin = new Padding(0, 6, 0, 10),
            Text = "Fields filled in for each parent: fee reminder " +
                   string.Join(" ", WhatsAppSettings.FeeFields.Select(f => "{" + f + "}")) + "; attendance " +
                   string.Join(" ", WhatsAppSettings.AttendanceFields.Select(f => "{" + f + "}")) + ".\n" +
                   "Install WhatsApp (or WhatsApp Business) on this PC and link it to the hostel's phone, or choose WhatsApp Web. " +
                   "Messages are sent from the hostel's own WhatsApp; no Meta account is needed.",
        };
        _manualSection = FormFields.CreateStack(manualFields, manualHelp);

        // ---- Meta API ----
        TableLayoutPanel fields = FormFields.CreateTable(labelWidth: 190, inputWidth: 380);
        _phoneNumberIdBox = FormFields.AddTextBox(fields, "Phone Number ID", 30, required: true);
        _accountIdBox = FormFields.AddTextBox(fields, "WhatsApp Business Account ID", 30);
        _tokenBox = FormFields.AddTextBox(fields, "Access token", 600, required: true);
        _tokenBox.UseSystemPasswordChar = true;
        _feeTemplateBox = FormFields.AddTextBox(fields, "Fee reminder template", 100, required: true);
        _attendanceTemplateBox = FormFields.AddTextBox(fields, "Attendance template", 100, required: true);
        _languageBox = FormFields.AddTextBox(fields, "Template language", 10, required: true);

        // ---- Payment QR image (both modes) ----
        TableLayoutPanel qrFields = FormFields.CreateTable(labelWidth: 190, inputWidth: 470);
        _qrLabel = new Label { AutoSize = true, MaximumSize = new Size(470, 0), Margin = new Padding(0, 8, 0, 4) };
        FormFields.AddRow(qrFields, "Payment QR image", _qrLabel);
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
        FormFields.AddRow(qrFields, string.Empty, FormFields.CreateButtonRow(chooseQrButton, _removeQrButton));

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
                   "• Payment QR image: only when the fee reminder template has an image header; otherwise remove it. " +
                   "(WhatsApp on this PC: Copy QR Image puts it on the clipboard to paste into the chat.)\n" +
                   "• Send Test Message sends Meta's \"hello_world\" message to check the settings.",
        };

        _apiSection = FormFields.CreateStack(fields, help, testRow);

        FlowLayoutPanel stack = FormFields.CreateStack(_manualRadio, _apiRadio, _manualSection, _apiSection, qrFields, saveRow);
        Controls.Add(stack);

        Load += (_, _) => LoadSettings();
    }

    private void LoadSettings()
    {
        try
        {
            WhatsAppSettings settings = WhatsAppSettingsService.Get();
            _manualRadio.Checked = settings.IsManual;
            _apiRadio.Checked = !settings.IsManual;
            _openInBox.SelectedIndex = settings.OpenIn == WhatsAppOpenIn.Web ? 1 : 0;
            _feeTextBox.Text = settings.FeeText.ReplaceLineEndings(Environment.NewLine);
            _attendanceTextBox.Text = settings.AttendanceText.ReplaceLineEndings(Environment.NewLine);
            ShowMode();
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

    private void ShowMode()
    {
        _manualSection.Visible = _manualRadio.Checked;
        _apiSection.Visible = !_manualRadio.Checked;
    }

    private void ShowQr(string? file)
    {
        _qrLabel.Text = file is null ? "None" : Path.GetFileName(file);
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
                Mode = _apiRadio.Checked ? WhatsAppMode.Api : WhatsAppMode.Manual,
                OpenIn = _openInBox.SelectedIndex == 1 ? WhatsAppOpenIn.Web : WhatsAppOpenIn.DesktopApp,
                FeeText = _feeTextBox.Text,
                AttendanceText = _attendanceTextBox.Text,
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
