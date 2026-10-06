using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using HostelManagement.Data;
using HostelManagement.Models;
using HostelManagement.Utilities;

namespace HostelManagement.Services;

/// <summary>
/// Reads and saves the email account and texts. The app password is encrypted with Windows (DPAPI) for the
/// signed in Windows user, so it cannot be read from the database file or a backup on another computer;
/// after restoring on another computer it has to be entered again.
/// </summary>
public static partial class EmailSettingsService
{
    /// <summary>The {Fields} that can be used in each kind of email.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> Fields { get; } = new Dictionary<string, IReadOnlyList<string>>
    {
        [EmailType.Invoice] = ["StudentName", "ParentName", "HostelName", "HostelPhone", "InvoiceNumber", "Period", "Amount", "DueDate", "Pending"],
        [EmailType.Receipt] = ["StudentName", "ParentName", "HostelName", "HostelPhone", "InvoiceNumber", "ReceiptNumber", "PaidAmount", "PaymentDate", "PaymentMethod", "Pending"],
        [EmailType.DueReminder] = ["StudentName", "ParentName", "HostelName", "HostelPhone", "InvoiceList", "Pending", "Overdue"],
    };

    private const string PasswordPrefix = "dpapi:";

    public static EmailSettings Get()
    {
        Dictionary<string, string> values = AppSettingRepository.GetAll();
        string Value(string key, string fallback) => values.TryGetValue(key, out string? v) && v.Length > 0 ? v : fallback;

        return new EmailSettings
        {
            SmtpHost = Value("Email.SmtpHost", EmailSettings.GmailHost),
            SmtpPort = int.TryParse(Value("Email.SmtpPort", ""), NumberStyles.None, CultureInfo.InvariantCulture, out int port) ? port : EmailSettings.GmailPort,
            SenderEmail = Value("Email.SenderEmail", ""),
            SenderName = Value("Email.SenderName", ""),
            AppPassword = Unprotect(Value("Email.AppPassword", "")),
            Invoice = new(Value("Email.Invoice.Subject", EmailSettings.DefaultInvoice.Subject), Value("Email.Invoice.Body", EmailSettings.DefaultInvoice.Body)),
            Receipt = new(Value("Email.Receipt.Subject", EmailSettings.DefaultReceipt.Subject), Value("Email.Receipt.Body", EmailSettings.DefaultReceipt.Body)),
            Reminder = new(Value("Email.Reminder.Subject", EmailSettings.DefaultReminder.Subject), Value("Email.Reminder.Body", EmailSettings.DefaultReminder.Body)),
        };
    }

    public static void Save(EmailSettings settings)
    {
        settings.SmtpHost = Validators.Clean(settings.SmtpHost);
        settings.SenderEmail = Validators.Clean(settings.SenderEmail);
        settings.SenderName = Validators.Clean(settings.SenderName);
        // Gmail shows the app password in groups of four letters; the spaces are not part of it.
        settings.AppPassword = settings.AppPassword.Replace(" ", string.Empty, StringComparison.Ordinal).Trim();
        Validate(settings);

        AppSettingRepository.SaveAll(new Dictionary<string, string>
        {
            ["Email.SmtpHost"] = settings.SmtpHost,
            ["Email.SmtpPort"] = settings.SmtpPort.ToString(CultureInfo.InvariantCulture),
            ["Email.SenderEmail"] = settings.SenderEmail,
            ["Email.SenderName"] = settings.SenderName,
            ["Email.AppPassword"] = Protect(settings.AppPassword),
            ["Email.Invoice.Subject"] = settings.Invoice.Subject.Trim(),
            ["Email.Invoice.Body"] = settings.Invoice.Body.Trim(),
            ["Email.Receipt.Subject"] = settings.Receipt.Subject.Trim(),
            ["Email.Receipt.Body"] = settings.Receipt.Body.Trim(),
            ["Email.Reminder.Subject"] = settings.Reminder.Subject.Trim(),
            ["Email.Reminder.Body"] = settings.Reminder.Body.Trim(),
        });
        AppLogger.Info("Email settings saved.");
    }

    /// <summary>Replaces each {Field} with its value.</summary>
    public static string Fill(string template, IReadOnlyDictionary<string, string> values) =>
        FieldPattern().Replace(template, m => values.TryGetValue(m.Groups[1].Value, out string? value) ? value : m.Value);

    private static void Validate(EmailSettings settings)
    {
        if (settings.SenderEmail.Length == 0)
        {
            throw new ValidationException("Please enter the Gmail address that sends the emails.");
        }
        if (!Validators.IsValidEmailOrEmpty(settings.SenderEmail))
        {
            throw new ValidationException("Please enter a valid sender email address.");
        }
        if (settings.AppPassword.Length == 0)
        {
            throw new ValidationException("Please enter the Gmail app password.");
        }
        if (settings.SmtpHost.Length == 0)
        {
            throw new ValidationException("Please enter the SMTP server.");
        }
        if (settings.SmtpPort is < 1 or > 65535)
        {
            throw new ValidationException("Please enter a valid SMTP port (587 for Gmail).");
        }
        Validators.CheckLength(settings.SenderName, 100, "Sender name");

        foreach ((string type, string name, EmailTemplate template) in new[]
                 {
                     (EmailType.Invoice, "invoice", settings.Invoice),
                     (EmailType.Receipt, "receipt", settings.Receipt),
                     (EmailType.DueReminder, "reminder", settings.Reminder),
                 })
        {
            if (template.Subject.Trim().Length == 0 || template.Body.Trim().Length == 0)
            {
                throw new ValidationException($"Please enter the subject and message of the {name} email.");
            }
            Validators.CheckLength(template.Subject.Trim(), 255, $"The {name} email subject");

            List<string> unknown = FieldPattern().Matches(template.Subject + template.Body)
                .Select(m => m.Groups[1].Value)
                .Where(field => !Fields[type].Contains(field))
                .Distinct()
                .ToList();
            if (unknown.Count > 0)
            {
                throw new ValidationException(
                    $"The {name} email uses {string.Join(", ", unknown.Select(f => "{" + f + "}"))}, which is not available. " +
                    $"Available: {string.Join(", ", Fields[type].Select(f => "{" + f + "}"))}.");
            }
        }
    }

    private static string Protect(string password) =>
        password.Length == 0
            ? string.Empty
            : PasswordPrefix + Convert.ToBase64String(
                ProtectedData.Protect(Encoding.UTF8.GetBytes(password), null, DataProtectionScope.CurrentUser));

    /// <summary>An empty password when it was saved by another Windows user or computer (it must be entered again).</summary>
    private static string Unprotect(string stored)
    {
        if (!stored.StartsWith(PasswordPrefix, StringComparison.Ordinal))
        {
            return string.Empty;
        }
        try
        {
            return Encoding.UTF8.GetString(ProtectedData.Unprotect(
                Convert.FromBase64String(stored[PasswordPrefix.Length..]), null, DataProtectionScope.CurrentUser));
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            AppLogger.Error("The saved Gmail app password could not be read on this computer.", ex);
            return string.Empty;
        }
    }

    [GeneratedRegex(@"\{([A-Za-z]+)\}")]
    private static partial Regex FieldPattern();
}
