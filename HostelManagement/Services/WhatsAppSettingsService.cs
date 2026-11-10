using System.Security.Cryptography;
using System.Text;
using HostelManagement.Data;
using HostelManagement.Models;
using HostelManagement.Utilities;

namespace HostelManagement.Services;

/// <summary>
/// Reads and saves the WhatsApp settings. The access token is encrypted with Windows (DPAPI) for the signed in
/// Windows user, like the Gmail app password: it cannot be read from the database file or a backup on another
/// computer, and after a restore elsewhere it has to be entered again. The payment QR image is copied into
/// Documents\WhatsApp, so backups include it.
/// </summary>
public static class WhatsAppSettingsService
{
    private const string TokenPrefix = "dpapi:";
    private static readonly string[] QrExtensions = [".png", ".jpg", ".jpeg"];
    private const long MaxQrBytes = 5 * 1024 * 1024;

    public static WhatsAppSettings Get()
    {
        Dictionary<string, string> values = AppSettingRepository.GetAll();
        string Value(string key, string fallback) => values.TryGetValue(key, out string? v) && v.Length > 0 ? v : fallback;

        return new WhatsAppSettings
        {
            PhoneNumberId = Value("WhatsApp.PhoneNumberId", ""),
            BusinessAccountId = Value("WhatsApp.BusinessAccountId", ""),
            AccessToken = Unprotect(Value("WhatsApp.AccessToken", "")),
            FeeTemplate = Value("WhatsApp.FeeTemplate", WhatsAppSettings.DefaultFeeTemplate),
            AttendanceTemplate = Value("WhatsApp.AttendanceTemplate", WhatsAppSettings.DefaultAttendanceTemplate),
            Language = Value("WhatsApp.Language", WhatsAppSettings.DefaultLanguage),
            PaymentQrPath = Value("WhatsApp.PaymentQr", ""),
        };
    }

    /// <summary>
    /// Saves the settings. An empty token keeps the saved one (the screen never shows it); a new QR image file is
    /// copied into the data folder, and <paramref name="removeQr"/> removes it.
    /// </summary>
    public static WhatsAppSettings Save(WhatsAppSettings input, string? newQrFile = null, bool removeQr = false)
    {
        WhatsAppSettings saved = Get();
        var settings = new WhatsAppSettings
        {
            PhoneNumberId = Validators.Clean(input.PhoneNumberId),
            BusinessAccountId = Validators.Clean(input.BusinessAccountId),
            AccessToken = input.AccessToken.Trim().Length > 0 ? input.AccessToken.Trim() : saved.AccessToken,
            FeeTemplate = Validators.Clean(input.FeeTemplate).ToLowerInvariant(),
            AttendanceTemplate = Validators.Clean(input.AttendanceTemplate).ToLowerInvariant(),
            Language = Validators.Clean(input.Language),
            PaymentQrPath = saved.PaymentQrPath,
        };
        Validate(settings);
        if (newQrFile is not null)
        {
            ValidateQr(newQrFile);
        }

        if (removeQr || newQrFile is not null)
        {
            StudentFileService.TryDelete(settings.PaymentQrPath);
            settings.PaymentQrPath = newQrFile is null ? string.Empty : StoreQr(newQrFile);
        }

        AppSettingRepository.SaveAll(new Dictionary<string, string>
        {
            ["WhatsApp.PhoneNumberId"] = settings.PhoneNumberId,
            ["WhatsApp.BusinessAccountId"] = settings.BusinessAccountId,
            ["WhatsApp.AccessToken"] = Protect(settings.AccessToken),
            ["WhatsApp.FeeTemplate"] = settings.FeeTemplate,
            ["WhatsApp.AttendanceTemplate"] = settings.AttendanceTemplate,
            ["WhatsApp.Language"] = settings.Language,
            ["WhatsApp.PaymentQr"] = settings.PaymentQrPath,
        });
        AppLogger.Info("WhatsApp settings saved.");
        return settings;
    }

    /// <summary>The full path of the saved payment QR image, or null when there is none.</summary>
    public static string? PaymentQrFile(WhatsAppSettings settings) =>
        StudentFileService.Exists(settings.PaymentQrPath) ? StudentFileService.FullPath(settings.PaymentQrPath) : null;

    private static void Validate(WhatsAppSettings settings)
    {
        if (settings.PhoneNumberId.Length == 0 || !settings.PhoneNumberId.All(char.IsAsciiDigit))
        {
            throw new ValidationException("Please enter the Phone Number ID (only digits) from Meta's WhatsApp API Setup page.");
        }
        if (settings.BusinessAccountId.Length > 0 && !settings.BusinessAccountId.All(char.IsAsciiDigit))
        {
            throw new ValidationException("The WhatsApp Business Account ID has only digits.");
        }
        if (settings.AccessToken.Length == 0)
        {
            throw new ValidationException("Please enter the access token of the system user (it starts with EAA).");
        }
        if (settings.AccessToken.Any(char.IsWhiteSpace) || settings.AccessToken.Length < 20)
        {
            throw new ValidationException("The access token does not look right: copy the whole token, without spaces.");
        }
        foreach ((string name, string what) in new[]
                 {
                     (settings.FeeTemplate, "fee reminder"), (settings.AttendanceTemplate, "attendance"),
                 })
        {
            if (name.Length == 0 || !name.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '_'))
            {
                throw new ValidationException(
                    $"Please enter the {what} template name exactly as in WhatsApp Manager (small letters, numbers and _).");
            }
        }
        if (settings.Language.Length == 0)
        {
            throw new ValidationException("Please enter the templates' language code (en for English).");
        }
        Validators.CheckLength(settings.PhoneNumberId, 30, "Phone Number ID");
        Validators.CheckLength(settings.BusinessAccountId, 30, "WhatsApp Business Account ID");
        Validators.CheckLength(settings.Language, 10, "Language code");
    }

    private static void ValidateQr(string file)
    {
        if (!File.Exists(file))
        {
            throw new ValidationException("The QR image file was not found.");
        }
        if (!QrExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
        {
            throw new ValidationException("Please choose the QR code as a PNG or JPG image.");
        }
        if (new FileInfo(file).Length > MaxQrBytes)
        {
            throw new ValidationException("The QR image is larger than 5 MB. Please choose a smaller image.");
        }
        try
        {
            using Image image = Image.FromFile(file);
        }
        catch (Exception ex) when (ex is OutOfMemoryException or ArgumentException or IOException)
        {
            throw new ValidationException("The QR file is not a readable image.");
        }
    }

    private static string StoreQr(string file)
    {
        string folder = Path.Combine(AppPaths.DataFolder, "Documents", "WhatsApp");
        Directory.CreateDirectory(folder);
        string name = $"PaymentQR_{DateTime.Now:yyyyMMddHHmmss}{Path.GetExtension(file).ToLowerInvariant()}";
        File.Copy(file, Path.Combine(folder, name), overwrite: true);
        return Path.Combine("Documents", "WhatsApp", name);
    }

    private static string Protect(string token) =>
        token.Length == 0
            ? string.Empty
            : TokenPrefix + Convert.ToBase64String(
                ProtectedData.Protect(Encoding.UTF8.GetBytes(token), null, DataProtectionScope.CurrentUser));

    /// <summary>An empty token when it was saved by another Windows user or computer (it must be entered again).</summary>
    private static string Unprotect(string stored)
    {
        if (!stored.StartsWith(TokenPrefix, StringComparison.Ordinal))
        {
            return string.Empty;
        }
        try
        {
            return Encoding.UTF8.GetString(ProtectedData.Unprotect(
                Convert.FromBase64String(stored[TokenPrefix.Length..]), null, DataProtectionScope.CurrentUser));
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            AppLogger.Error("The saved WhatsApp access token could not be read on this computer.", ex);
            return string.Empty;
        }
    }
}
