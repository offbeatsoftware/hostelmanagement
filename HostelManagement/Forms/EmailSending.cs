using HostelManagement.Models;
using HostelManagement.Services;
using HostelManagement.Utilities;

namespace HostelManagement.Forms;

/// <summary>
/// Sends prepared emails from a screen: Gmail is contacted in the background so the window stays responsive,
/// and each result is written to the email history (on the screen's thread) as soon as it is known.
/// </summary>
internal static class EmailSending
{
    /// <summary>Sends the emails one by one and shows one summary message at the end.</summary>
    public static async Task SendAsync(Control screen, IReadOnlyList<OutgoingEmail> emails, IReadOnlyList<string>? problems = null)
    {
        EmailSettings settings = EmailService.GetConfiguredSettings();
        var failures = new List<string>(problems ?? []);
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
            foreach (OutgoingEmail email in emails)
            {
                string? error = await Task.Run(() => EmailService.Send(settings, email));
                EmailService.Log(email, error);
                if (error is null)
                {
                    sent++;
                }
                else
                {
                    failures.Add($"{email.StudentName} ({email.RecipientEmail}): {error}");
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

        ShowSummary(emails, sent, failures);
    }

    private static void ShowSummary(IReadOnlyList<OutgoingEmail> emails, int sent, List<string> failures)
    {
        if (failures.Count == 0)
        {
            Dialogs.Info(emails.Count == 1
                ? $"The email was sent to {emails[0].RecipientName} ({emails[0].RecipientEmail})."
                : $"{sent} emails were sent.");
            return;
        }

        const int maxListed = 10;
        string list = string.Join(Environment.NewLine, failures.Take(maxListed).Select(f => "• " + f));
        if (failures.Count > maxListed)
        {
            list += $"{Environment.NewLine}… and {failures.Count - maxListed} more (see the email history in Email Settings).";
        }
        string header = sent > 0 ? $"{sent} emails were sent, {failures.Count} were not:" : "The email could not be sent:";
        Dialogs.Warning(header + Environment.NewLine + Environment.NewLine + list);
    }
}
