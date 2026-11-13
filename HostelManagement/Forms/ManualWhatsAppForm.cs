using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using HostelManagement.Models;
using HostelManagement.Services;
using HostelManagement.Utilities;

namespace HostelManagement.Forms;

/// <summary>
/// Manual WhatsApp (client decision): goes through the prepared messages one parent at a time. Open in WhatsApp opens
/// the parent's chat in WhatsApp on this PC (or WhatsApp Web) with the message typed; the admin presses Send there and
/// then Sent - Next here, which records it in the history. Nothing is sent without the admin.
/// </summary>
public sealed class ManualWhatsAppForm : Form
{
    private readonly IReadOnlyList<WhatsAppMessage> _messages;
    private readonly WhatsAppSettings _settings;
    private readonly string? _qrFile;
    private readonly Label _headingLabel;
    private readonly TextBox _textBox;
    private readonly Button _openButton;
    private readonly Button _copyQrButton;
    private readonly Button _sentButton;
    private readonly Button _skipButton;
    private readonly Label _progressLabel;
    private int _index;

    public ManualWhatsAppForm(IReadOnlyList<WhatsAppMessage> messages, WhatsAppSettings settings, string what)
    {
        _messages = messages;
        _settings = settings;
        _qrFile = messages.Any(m => m.MessageType == EmailType.WhatsAppReminder) ? WhatsAppSettingsService.PaymentQrFile(settings) : null;

        Text = $"Send WhatsApp {what}";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Font;
        Font = UiTheme.BodyFont;
        BackColor = Color.White;
        ClientSize = new Size(720, 600);

        var steps = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(680, 0),
            ForeColor = UiTheme.TextMuted,
            Margin = new Padding(0, 0, 0, 8),
            Text = _qrFile is null
                ? "1. Click Open in WhatsApp: the parent's chat opens with this message typed.\n" +
                  "2. Check it and press Send in WhatsApp.\n" +
                  "3. Come back and click Sent - Next."
                : "1. Click Open in WhatsApp: the parent's chat opens with this message typed, and the payment QR image is copied.\n" +
                  "2. Press Send in WhatsApp, then Ctrl+V and Send again to add the QR code below the message.\n" +
                  "3. Come back and click Sent - Next.",
        };
        _headingLabel = new Label { AutoSize = true, MaximumSize = new Size(680, 0), Font = UiTheme.BodyBoldFont, Margin = new Padding(0, 4, 0, 6) };
        _textBox = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            BackColor = Color.White,
            Size = new Size(680, 300),
            Margin = new Padding(0, 0, 0, 6),
        };

        _openButton = new Button { Text = "Open in WhatsApp" };
        UiTheme.StylePrimaryButton(_openButton);
        _openButton.Width = 170;
        _openButton.Click += (_, _) => OpenChat();
        var copyTextButton = new Button { Text = "Copy Text" };
        UiTheme.StyleSecondaryButton(copyTextButton);
        copyTextButton.Width = 110;
        copyTextButton.Click += (_, _) => Clipboard.SetText(_textBox.Text);
        _copyQrButton = new Button { Text = "Copy QR Image", Visible = _qrFile is not null };
        UiTheme.StyleSecondaryButton(_copyQrButton);
        _copyQrButton.Width = 140;
        _copyQrButton.Click += (_, _) => CopyQr();
        FlowLayoutPanel openRow = FormFields.CreateButtonRow(_openButton, copyTextButton, _copyQrButton);

        _sentButton = new Button { Text = "Sent - Next" };
        UiTheme.StylePrimaryButton(_sentButton);
        _sentButton.Width = 140;
        _sentButton.Click += (_, _) => Next(sent: true);
        _skipButton = new Button { Text = "Skip" };
        UiTheme.StyleSecondaryButton(_skipButton);
        _skipButton.Click += (_, _) => Next(sent: false);
        var closeButton = new Button { Text = "Close", DialogResult = DialogResult.Cancel };
        UiTheme.StyleSecondaryButton(closeButton);
        CancelButton = closeButton;
        _progressLabel = FormFields.CreateMessageLabel();
        _progressLabel.ForeColor = UiTheme.TextMuted;
        FlowLayoutPanel doneRow = FormFields.CreateButtonRow(_sentButton, _skipButton, closeButton, _progressLabel);

        FlowLayoutPanel stack = FormFields.CreateStack(steps, _headingLabel, _textBox, openRow, doneRow);
        stack.Location = new Point(18, 14);
        Controls.Add(stack);

        ShowCurrent();
    }

    public int SentCount { get; private set; }
    public int SkippedCount { get; private set; }

    /// <summary>The messages not reached when the window was closed.</summary>
    public int NotDoneCount => _messages.Count - SentCount - SkippedCount;

    private WhatsAppMessage Current => _messages[_index];

    private void ShowCurrent()
    {
        WhatsAppMessage message = Current;
        _headingLabel.Text = $"{_index + 1} of {_messages.Count}: {message.StudentName}  >  {message.Relation} {message.RecipientName} " +
                             $"(+{message.Mobile})";
        _textBox.Text = message.Text.ReplaceLineEndings(Environment.NewLine);
        _progressLabel.Text = $"{SentCount} sent, {SkippedCount} skipped";
        _openButton.Focus();
    }

    private void OpenChat()
    {
        // The chat link can only carry text, so the QR image goes on the clipboard, ready for Ctrl+V in the chat.
        CopyQr();
        string link = WhatsAppService.ChatLink(_settings.OpenIn, Current.Mobile, Current.Text);
        try
        {
            Process.Start(new ProcessStartInfo(link) { UseShellExecute = true });
        }
        catch (Win32Exception ex) when (_settings.OpenIn == WhatsAppOpenIn.DesktopApp)
        {
            // The WhatsApp app is not installed (or not signed in): offer WhatsApp Web instead.
            AppLogger.Error("The WhatsApp app could not be opened.", ex);
            if (Dialogs.Confirm("The WhatsApp app could not be opened on this PC. Open the chat in WhatsApp Web (in the browser) instead?"))
            {
                Process.Start(new ProcessStartInfo(WhatsAppService.ChatLink(WhatsAppOpenIn.Web, Current.Mobile, Current.Text))
                    { UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "WhatsApp could not be opened.");
        }
    }

    private void CopyQr()
    {
        if (_qrFile is null)
        {
            return;
        }
        try
        {
            using var stream = new MemoryStream(File.ReadAllBytes(_qrFile));
            using Image image = Image.FromStream(stream);
            Clipboard.SetImage(new Bitmap(image));
            _progressLabel.Text = "QR copied: after Send, press Ctrl+V and Send.";
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or ExternalException)
        {
            ErrorHandler.Handle(ex, "The QR image could not be copied.");
        }
    }

    private void Next(bool sent)
    {
        try
        {
            if (sent)
            {
                WhatsAppService.LogManual(Current);
                SentCount++;
            }
            else
            {
                SkippedCount++;
            }
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The message could not be recorded in the history.");
            return;
        }

        if (_index + 1 < _messages.Count)
        {
            _index++;
            ShowCurrent();
            return;
        }
        DialogResult = DialogResult.OK;
        Close();
    }
}
