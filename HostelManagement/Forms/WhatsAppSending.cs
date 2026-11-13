using HostelManagement.Models;
using HostelManagement.Services;
using HostelManagement.Utilities;

namespace HostelManagement.Forms;

/// <summary>
/// Sends prepared WhatsApp messages from a screen: WhatsApp is contacted in the background so the window stays
/// responsive, and each result is written to the history (on the screen's thread) as soon as it is known.
/// </summary>
internal static class WhatsAppSending
{
    /// <summary>Asks for confirmation (with the first message as an example), sends one by one and shows a summary.</summary>
    public static async Task SendAsync(Control screen, IReadOnlyList<WhatsAppMessage> messages, IReadOnlyList<string> problems,
        string what)
    {
        if (messages.Count == 0)
        {
            Dialogs.Warning(problems.Count > 0
                ? "No WhatsApp message can be sent:" + Environment.NewLine + Environment.NewLine + List(problems)
                : "There is nobody to send a WhatsApp message to.");
            return;
        }

        WhatsAppSettings current = WhatsAppSettingsService.Get();
        if (current.IsManual)
        {
            SendManually(screen, messages, problems, what, current);
            return;
        }

        WhatsAppMessage first = messages[0];
        string skipped = problems.Count > 0 ? $"{Environment.NewLine}{problems.Count} student(s) cannot be messaged and will be listed afterwards." : "";
        if (!Dialogs.Confirm(
                $"Send the WhatsApp {what} to the parents of {messages.Count} student(s)?{skipped}{Environment.NewLine}{Environment.NewLine}" +
                $"Example, to {first.Relation.ToLowerInvariant()} {first.RecipientName} (+{first.Mobile}):{Environment.NewLine}{Environment.NewLine}" +
                first.Preview))
        {
            return;
        }

        WhatsAppSettings settings = WhatsAppService.GetConfiguredSettings();
        var failures = new List<string>(problems);
        int sent = 0;

        Form? window = screen.FindForm();
        Cursor? previousCursor = window?.Cursor;
        screen.Enabled = false;
        if (window is not null)
        {
            window.Cursor = Cursors.WaitCursor;
        }

        try
        {
            // The payment QR image is uploaded once for the whole sending.
            string? qrMediaId = null;
            if (messages.Any(m => m.WithPaymentQr))
            {
                try
                {
                    qrMediaId = await Task.Run(() => WhatsAppService.UploadPaymentQr(settings));
                }
                catch (Exception ex)
                {
                    AppLogger.Error("The payment QR image could not be uploaded to WhatsApp.", ex);
                    Dialogs.Warning("The payment QR image could not be sent to WhatsApp, so no reminder was sent:" +
                                    Environment.NewLine + WhatsAppService.FriendlyError(ex));
                    return;
                }
            }

            foreach (WhatsAppMessage message in messages)
            {
                string? error = await Task.Run(() => WhatsAppService.Send(settings, message, qrMediaId));
                WhatsAppService.Log(message, error);
                if (error is null)
                {
                    sent++;
                }
                else
                {
                    failures.Add($"{message.StudentName} (+{message.Mobile}): {error}");
                }
            }
        }
        finally
        {
            screen.Enabled = true;
            if (window is not null)
            {
                window.Cursor = previousCursor ?? Cursors.Default;
            }
        }

        if (failures.Count == 0)
        {
            Dialogs.Info(messages.Count == 1
                ? $"The WhatsApp message was sent to {first.RecipientName} (+{first.Mobile})."
                : $"{sent} WhatsApp messages were sent.");
            return;
        }
        string header = sent > 0 ? $"{sent} WhatsApp messages were sent, {failures.Count} were not:" : "The WhatsApp messages could not be sent:";
        Dialogs.Warning(header + Environment.NewLine + Environment.NewLine + List(failures));
    }

    /// <summary>Manual mode: the admin sends each message from WhatsApp on this PC, one parent at a time.</summary>
    private static void SendManually(Control screen, IReadOnlyList<WhatsAppMessage> messages, IReadOnlyList<string> problems,
        string what, WhatsAppSettings settings)
    {
        using var window = new ManualWhatsAppForm(messages, settings, what);
        window.ShowDialog(screen.FindForm());

        var summary = new List<string> { $"{window.SentCount} sent, {window.SkippedCount} skipped" + (window.NotDoneCount > 0 ? $", {window.NotDoneCount} not done." : ".") };
        if (problems.Count > 0)
        {
            summary.Add(string.Empty);
            summary.Add("These students could not be messaged:");
            summary.Add(List(problems));
        }
        if (problems.Count > 0 || window.NotDoneCount > 0 || window.SkippedCount > 0)
        {
            Dialogs.Info(string.Join(Environment.NewLine, summary));
        }
    }

    private static string List(IReadOnlyList<string> lines)
    {
        const int maxListed = 10;
        string list = string.Join(Environment.NewLine, lines.Take(maxListed).Select(l => "• " + l));
        return lines.Count > maxListed
            ? $"{list}{Environment.NewLine}… and {lines.Count - maxListed} more (see the history in Email Settings)."
            : list;
    }
}
