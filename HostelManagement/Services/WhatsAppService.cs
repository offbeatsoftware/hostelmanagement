using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using HostelManagement.Data;
using HostelManagement.Models;
using HostelManagement.Utilities;

namespace HostelManagement.Services;

/// <summary>An error returned by Meta's WhatsApp Cloud API, with Meta's error code.</summary>
public sealed class WhatsAppApiException(int code, string message) : Exception(message)
{
    public int Code { get; } = code;
}

/// <summary>Talks to WhatsApp. Replaced in the automated tests so that no real message is sent or charged.</summary>
public interface IWhatsAppSender
{
    /// <summary>Uploads an image to WhatsApp and returns its media ID (valid for 30 days).</summary>
    string UploadImage(WhatsAppSettings settings, string file);

    /// <summary>Sends a template message and returns Meta's message ID.</summary>
    string SendTemplate(WhatsAppSettings settings, string mobile, string template, string language,
        IReadOnlyList<string> parameters, string? headerImageId);
}

/// <summary>Sends through Meta's WhatsApp Cloud API (graph.facebook.com) with the system user's access token.</summary>
public sealed class CloudApiWhatsAppSender : IWhatsAppSender
{
    public const string ApiVersion = "v23.0";
    private static readonly HttpClient Http = new() { BaseAddress = new Uri("https://graph.facebook.com/"), Timeout = TimeSpan.FromSeconds(30) };

    public string UploadImage(WhatsAppSettings settings, string file)
    {
        using var content = new MultipartFormDataContent
        {
            { new StringContent("whatsapp"), "messaging_product" },
        };
        var image = new ByteArrayContent(File.ReadAllBytes(file));
        string mime = Path.GetExtension(file).Equals(".png", StringComparison.OrdinalIgnoreCase) ? "image/png" : "image/jpeg";
        image.Headers.ContentType = new MediaTypeHeaderValue(mime);
        content.Add(new StringContent(mime), "type");
        content.Add(image, "file", Path.GetFileName(file));

        JsonNode reply = Post(settings, $"{ApiVersion}/{settings.PhoneNumberId}/media", content);
        return reply["id"]?.GetValue<string>() ?? throw new WhatsAppApiException(0, "WhatsApp did not return a media ID.");
    }

    public string SendTemplate(WhatsAppSettings settings, string mobile, string template, string language,
        IReadOnlyList<string> parameters, string? headerImageId)
    {
        string json = BuildTemplatePayload(mobile, template, language, parameters, headerImageId);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        JsonNode reply = Post(settings, $"{ApiVersion}/{settings.PhoneNumberId}/messages", content);
        return reply["messages"]?[0]?["id"]?.GetValue<string>() ?? string.Empty;
    }

    /// <summary>The request body of a template message: header image (optional) and the body's {{1}}, {{2}}... in order.</summary>
    public static string BuildTemplatePayload(string mobile, string template, string language, IReadOnlyList<string> parameters,
        string? headerImageId)
    {
        var components = new JsonArray();
        if (headerImageId is not null)
        {
            components.Add(new JsonObject
            {
                ["type"] = "header",
                ["parameters"] = new JsonArray(new JsonObject
                {
                    ["type"] = "image",
                    ["image"] = new JsonObject { ["id"] = headerImageId },
                }),
            });
        }
        if (parameters.Count > 0)
        {
            var values = new JsonArray();
            foreach (string value in parameters)
            {
                values.Add(new JsonObject { ["type"] = "text", ["text"] = value });
            }
            components.Add(new JsonObject { ["type"] = "body", ["parameters"] = values });
        }

        var body = new JsonObject
        {
            ["messaging_product"] = "whatsapp",
            ["recipient_type"] = "individual",
            ["to"] = mobile,
            ["type"] = "template",
            ["template"] = new JsonObject
            {
                ["name"] = template,
                ["language"] = new JsonObject { ["code"] = language },
                ["components"] = components,
            },
        };
        return body.ToJsonString();
    }

    private static JsonNode Post(WhatsAppSettings settings, string path, HttpContent content)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.AccessToken);
        using HttpResponseMessage response = Http.Send(request);
        using var reader = new StreamReader(response.Content.ReadAsStream());
        string text = reader.ReadToEnd();

        JsonNode? reply = null;
        try
        {
            reply = JsonNode.Parse(text);
        }
        catch (JsonException)
        {
            // Not JSON (for example a proxy page): reported below with the HTTP status.
        }
        if (reply?["error"] is JsonNode error)
        {
            int code = error["code"]?.GetValue<int>() ?? 0;
            string message = error["error_data"]?["details"]?.GetValue<string>() ?? error["message"]?.GetValue<string>() ?? "Unknown error";
            throw new WhatsAppApiException(code, message);
        }
        if (!response.IsSuccessStatusCode || reply is null)
        {
            throw new WhatsAppApiException((int)response.StatusCode, $"WhatsApp answered with HTTP {(int)response.StatusCode}.");
        }
        return reply;
    }
}

/// <summary>
/// WhatsApp messages to parents (client decisions, version 1.4) as Meta-approved templates from the hostel's number:
/// the fee reminder goes to the father's mobile, or the mother's when the father has none, with the payment QR
/// image when one is set; the attendance message goes to the mother's mobile, or the father's. Messages are prepared
/// and logged on the screen's thread; only <see cref="Send"/> talks to WhatsApp, in the background.
/// </summary>
public static class WhatsAppService
{
    public static IWhatsAppSender Sender { get; set; } = new CloudApiWhatsAppSender();

    /// <summary>
    /// The agreed template wording (as submitted to Meta), for the preview. {{1}}... are filled with the message's
    /// parameters; the bank details and QR line are fixed text in the approved template.
    /// </summary>
    public const string FeeReminderText =
        "Dear {{1}},\nThis is a fee reminder from {{2}} for {{3}} for the academic year {{4}}.\nTotal fee: ₹{{5}}\n" +
        "Paid: ₹{{6}}\nPending: ₹{{7}}\n\n(Bank account details and the payment QR code, as in the approved template.)\n\n" +
        "After paying, please share the payment receipt or UTR number on {{8}}. Thank you.";

    public const string AttendanceText =
        "Dear {{1}},\nThis is to inform you that {{2}} was absent from {{3}} on {{4}}.\n" +
        "For any question, please call {{5}}. Thank you.";

    /// <summary>
    /// A mobile number as WhatsApp needs it: country code and digits only. A 10 digit Indian mobile gets 91 in
    /// front; a leading 0 or +91 is handled. Null when it is not a usable mobile number.
    /// </summary>
    public static string? ToWhatsAppNumber(string? mobile)
    {
        string digits = new((mobile ?? string.Empty).Where(char.IsAsciiDigit).ToArray());
        bool international = (mobile ?? string.Empty).TrimStart().StartsWith('+');
        if (digits.Length == 11 && digits[0] == '0')
        {
            digits = digits[1..];
        }
        if (digits.Length == 10 && digits[0] is >= '6' and <= '9')
        {
            return "91" + digits;
        }
        if (digits.Length == 12 && digits.StartsWith("91", StringComparison.Ordinal) && digits[2] is >= '6' and <= '9')
        {
            return digits;
        }
        // Another country's number, written with + and its country code.
        return international && digits.Length is >= 11 and <= 15 && !digits.StartsWith("91", StringComparison.Ordinal) ? digits : null;
    }

    /// <summary>Who gets the fee reminder: the father's mobile, else the mother's.</summary>
    public static (string Name, string Mobile, string Relation)? FeeRecipient(Student student) =>
        Recipient(student, fatherFirst: true);

    /// <summary>Who gets the attendance message: the mother's mobile, else the father's.</summary>
    public static (string Name, string Mobile, string Relation)? AttendanceRecipient(Student student) =>
        Recipient(student, fatherFirst: false);

    private static (string Name, string Mobile, string Relation)? Recipient(Student student, bool fatherFirst)
    {
        var father = (Name: student.FatherName, Mobile: ToWhatsAppNumber(student.FatherMobile), Relation: "Father");
        var mother = (Name: student.MotherName, Mobile: ToWhatsAppNumber(student.MotherMobile), Relation: "Mother");
        foreach (var parent in fatherFirst ? new[] { father, mother } : new[] { mother, father })
        {
            if (parent.Mobile is string mobile)
            {
                return (parent.Name.Length > 0 ? parent.Name : "Parent", mobile, parent.Relation);
            }
        }
        return null;
    }

    public static WhatsAppSettings GetConfiguredSettings()
    {
        WhatsAppSettings settings = WhatsAppSettingsService.Get();
        if (!settings.IsConfigured)
        {
            throw new ValidationException(
                "WhatsApp is not set up yet (or its access token must be entered again on this computer). " +
                "Open Email Settings > WhatsApp first.");
        }
        return settings;
    }

    /// <summary>
    /// Fee reminders for the chosen students. Students who cannot be messaged (no pending fee, no usable mobile)
    /// are returned as problems instead of stopping the others.
    /// </summary>
    public static (List<WhatsAppMessage> Messages, List<string> Problems) PrepareFeeReminders(IEnumerable<StudentDue> dues)
    {
        WhatsAppSettings settings = GetConfiguredSettings();
        bool withQr = WhatsAppSettingsService.PaymentQrFile(settings) is not null;
        var messages = new List<WhatsAppMessage>();
        var problems = new List<string>();
        foreach (StudentDue due in dues)
        {
            if (due.PendingAmount <= 0)
            {
                problems.Add($"{due.StudentName}: no pending fee.");
                continue;
            }
            Student? student = StudentService.GetStudent(due.StudentId);
            if (student is null || FeeRecipient(student) is not { } recipient)
            {
                problems.Add($"{due.StudentName}: neither the father nor the mother has a valid mobile number.");
                continue;
            }
            Hostel? hostel = HostelService.GetHostel(student.HostelId);
            (string name, string mobile, string relation) = recipient;
            string[] parameters =
            [
                name,
                hostel?.HostelName ?? AppInfo.BusinessName,
                student.StudentName,
                due.YearsText,
                Amount(due.TotalAmount),
                Amount(due.PaidAmount),
                Amount(due.PendingAmount),
                ContactNumber(hostel),
            ];
            messages.Add(new WhatsAppMessage
            {
                MessageType = EmailType.WhatsAppReminder,
                StudentId = student.StudentId,
                StudentName = student.StudentName,
                InvoiceIds = due.Invoices.Select(i => i.InvoiceId).ToList(),
                RecipientName = name,
                Relation = relation,
                Mobile = mobile,
                TemplateName = settings.FeeTemplate,
                Parameters = parameters,
                WithPaymentQr = withQr,
                HistoryText = settings.FeeTemplate,
                Preview = Fill(FeeReminderText, parameters),
            });
        }
        return (messages, problems);
    }

    /// <summary>
    /// Attendance messages for the absent students of a saved date. Students already messaged on WhatsApp for that
    /// date are left out unless <paramref name="includeAlreadySent"/>.
    /// </summary>
    public static (List<WhatsAppMessage> Messages, List<string> Problems) PrepareAbsences(int hostelId, DateTime date,
        bool includeAlreadySent = false)
    {
        WhatsAppSettings settings = GetConfiguredSettings();
        AttendanceSheet sheet = AttendanceService.GetSheet(hostelId, date);
        Hostel hostel = HostelService.GetHostel(hostelId) ?? throw new ValidationException("Please select the hostel.");
        string dateText = sheet.Date.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);
        string historyText = $"{settings.AttendanceTemplate} {dateText}";
        HashSet<int> alreadySent = includeAlreadySent ? [] : EmailHistoryRepository.GetStudentsSent(EmailType.WhatsAppAbsence, historyText);

        var messages = new List<WhatsAppMessage>();
        var problems = new List<string>();
        foreach (AttendanceEntry entry in sheet.Entries.Where(e => e.IsSaved && !e.IsPresent && !alreadySent.Contains(e.StudentId)))
        {
            Student? student = StudentService.GetStudent(entry.StudentId);
            if (student is null || AttendanceRecipient(student) is not { } recipient)
            {
                problems.Add($"{entry.StudentName}: neither the mother nor the father has a valid mobile number.");
                continue;
            }
            (string name, string mobile, string relation) = recipient;
            string[] parameters = [name, student.StudentName, hostel.HostelName, dateText, ContactNumber(hostel)];
            messages.Add(new WhatsAppMessage
            {
                MessageType = EmailType.WhatsAppAbsence,
                StudentId = student.StudentId,
                StudentName = student.StudentName,
                RecipientName = name,
                Relation = relation,
                Mobile = mobile,
                TemplateName = settings.AttendanceTemplate,
                Parameters = parameters,
                HistoryText = historyText,
                Preview = Fill(AttendanceText, parameters),
            });
        }
        return (messages, problems);
    }

    /// <summary>Uploads the payment QR image for this sending; null when there is none. Throws on a WhatsApp error.</summary>
    public static string? UploadPaymentQr(WhatsAppSettings settings) =>
        WhatsAppSettingsService.PaymentQrFile(settings) is string file ? Sender.UploadImage(settings, file) : null;

    /// <summary>Sends one message. Returns null when WhatsApp accepted it, otherwise a message the admin can act on.</summary>
    public static string? Send(WhatsAppSettings settings, WhatsAppMessage message, string? qrMediaId)
    {
        try
        {
            string id = Sender.SendTemplate(settings, message.Mobile, message.TemplateName, settings.Language, message.Parameters,
                message.WithPaymentQr ? qrMediaId : null);
            AppLogger.Info($"WhatsApp {message.TemplateName} to {Masked(message.Mobile)} accepted ({id}).");
            return null;
        }
        catch (Exception ex)
        {
            AppLogger.Error($"WhatsApp {message.TemplateName} to {Masked(message.Mobile)} failed.", ex);
            return FriendlyError(ex);
        }
    }

    /// <summary>Keeps one history row per invoice the message was about (in the email history).</summary>
    public static void Log(WhatsAppMessage message, string? error)
    {
        IEnumerable<int?> invoiceIds = message.InvoiceIds.Count > 0 ? message.InvoiceIds.Select(id => (int?)id) : [null];
        foreach (int? invoiceId in invoiceIds)
        {
            EmailHistoryRepository.Insert(new EmailHistoryEntry
            {
                StudentId = message.StudentId,
                InvoiceId = invoiceId,
                RecipientEmail = "+" + message.Mobile,
                EmailType = message.MessageType,
                Subject = message.HistoryText,
                SentDate = DateTime.Now,
                Status = error is null ? EmailStatus.Sent : EmailStatus.Failed,
                ErrorMessage = error ?? string.Empty,
            });
        }
    }

    /// <summary>
    /// Sends Meta's ready-made "hello_world" template to a mobile number, to check the settings. Returns null when
    /// sent, otherwise the problem.
    /// </summary>
    public static string? SendTest(WhatsAppSettings settings, string toMobile)
    {
        if (ToWhatsAppNumber(toMobile) is not string mobile)
        {
            return "Please enter a valid mobile number to send the test to.";
        }
        try
        {
            Sender.SendTemplate(settings, mobile, "hello_world", "en_US", [], null);
            return null;
        }
        catch (Exception ex)
        {
            AppLogger.Error("The WhatsApp test message failed.", ex);
            return FriendlyError(ex);
        }
    }

    /// <summary>A message the admin can act on, for Meta's usual error codes.</summary>
    public static string FriendlyError(Exception ex) => ex switch
    {
        WhatsAppApiException { Code: 190 } =>
            "WhatsApp did not accept the access token (wrong, expired or revoked). Enter a new token in Email Settings > WhatsApp.",
        WhatsAppApiException { Code: 10 or 200 or 3 } =>
            "The access token has no permission to send WhatsApp messages. Generate it with whatsapp_business_messaging.",
        WhatsAppApiException { Code: 100 or 33 } api when api.Message.Contains("phone", StringComparison.OrdinalIgnoreCase) ||
                                                            api.Message.Contains("does not exist", StringComparison.OrdinalIgnoreCase) =>
            "WhatsApp does not know the Phone Number ID. Check it in Email Settings > WhatsApp (Meta: WhatsApp > API Setup).",
        WhatsAppApiException { Code: 131026 } =>
            "This number cannot receive WhatsApp messages (not on WhatsApp, or an old WhatsApp version).",
        WhatsAppApiException { Code: 131030 } =>
            "This number is not on the allowed list of Meta's test number. Add it in API Setup, or use the hostel's own number.",
        WhatsAppApiException { Code: 132001 } =>
            "The template was not found. Check its name and language in Email Settings > WhatsApp, and that Meta approved it.",
        WhatsAppApiException { Code: 132000 or 132012 or 132018 } =>
            "The message does not match the approved template (number of values or the QR image header). " +
            "Check the template in WhatsApp Manager and the QR image setting.",
        WhatsAppApiException { Code: 132015 or 132016 } => "The template is paused or disabled by Meta. Check it in WhatsApp Manager.",
        WhatsAppApiException { Code: 130429 or 131056 or 4 or 80007 } =>
            "Too many messages at once. Wait a few minutes and send the rest again.",
        WhatsAppApiException { Code: 131048 or 131049 } =>
            "WhatsApp did not deliver this message to keep messaging healthy (too many messages to this parent, or low quality). Try again tomorrow.",
        WhatsAppApiException { Code: 131042 } => "There is a problem with the payment method in WhatsApp Manager. Check it there.",
        WhatsAppApiException { Code: 368 or 131031 } => "The WhatsApp account is restricted by Meta. Check WhatsApp Manager.",
        WhatsAppApiException { Code: 133010 } => "The hostel's number is not registered for WhatsApp messages yet. Finish its setup in WhatsApp Manager.",
        WhatsAppApiException api => $"WhatsApp did not send the message: {api.Message} (code {api.Code}).",
        HttpRequestException or TaskCanceledException or IOException =>
            "Could not connect to WhatsApp. Check the internet connection and try again.",
        _ => "The WhatsApp message could not be sent. Technical details were written to the log file.",
    };

    /// <summary>The template text with {{1}}, {{2}}... replaced by the values.</summary>
    public static string Fill(string text, IReadOnlyList<string> values)
    {
        for (int i = values.Count; i >= 1; i--)
        {
            text = text.Replace("{{" + i + "}}", values[i - 1], StringComparison.Ordinal);
        }
        return text;
    }

    /// <summary>"62,000" (Indian grouping, no ₹: the template has it), with paise only when there are any.</summary>
    private static string Amount(decimal amount) =>
        amount.ToString(amount == decimal.Truncate(amount) ? "N0" : "N2", Money.Culture);

    /// <summary>The number parents are asked to call: the hostel's phone, else the admin's.</summary>
    private static string ContactNumber(Hostel? hostel)
    {
        string phone = hostel?.Phone ?? string.Empty;
        if (phone.Length == 0)
        {
            phone = AdminUserRepository.GetFirst()?.Phone ?? string.Empty;
        }
        return phone.Length > 0 ? phone : "the hostel office";
    }

    private static string Masked(string mobile) => mobile.Length > 4 ? new string('x', mobile.Length - 4) + mobile[^4..] : mobile;
}
