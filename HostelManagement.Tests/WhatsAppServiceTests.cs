using System.Drawing.Imaging;
using System.Text.Json.Nodes;
using HostelManagement.Data;
using HostelManagement.Models;
using HostelManagement.Services;
using Xunit;

namespace HostelManagement.Tests;

/// <summary>WhatsApp fee reminders and attendance messages (version 1.4), sent through a fake: nothing reaches WhatsApp.</summary>
public sealed class WhatsAppServiceTests : TestDatabase
{
    private sealed class FakeWhatsApp : IWhatsAppSender
    {
        public List<(string Mobile, string Template, string Language, IReadOnlyList<string> Parameters, string? Header)> Sent { get; } = [];
        public List<string> Uploaded { get; } = [];
        public Dictionary<string, Exception> FailFor { get; } = [];

        public string UploadImage(WhatsAppSettings settings, string file)
        {
            Uploaded.Add(file);
            return "media-" + Uploaded.Count;
        }

        public string SendTemplate(WhatsAppSettings settings, string mobile, string template, string language,
            IReadOnlyList<string> parameters, string? headerImageId)
        {
            if (FailFor.TryGetValue(mobile, out Exception? error))
            {
                throw error;
            }
            Sent.Add((mobile, template, language, parameters, headerImageId));
            return "wamid.test" + Sent.Count;
        }
    }

    private const string Token = "EAAG000TEST0TOKEN0ONLY0FOR0AUTOMATED0TESTS0xyz";
    private readonly FakeWhatsApp _whatsApp = new();

    public WhatsAppServiceTests()
    {
        WhatsAppService.Sender = _whatsApp;
    }

    private static WhatsAppSettings SetUpWhatsApp(string? qrFile = null) =>
        WhatsAppSettingsService.Save(new WhatsAppSettings
        {
            PhoneNumberId = "1413266881875222",
            BusinessAccountId = "1510854370884023",
            AccessToken = Token,
            FeeTemplate = "fee_reminder",
            AttendanceTemplate = "attendance_alert",
            Language = "en",
        }, qrFile);

    private string CreateQr()
    {
        string file = Path.Combine(DataFolder, "qr.png");
        using var bitmap = new Bitmap(60, 60);
        bitmap.Save(file, ImageFormat.Png);
        return file;
    }

    /// <summary>A student in a room with a fee of 50,000 + 12,000 transport, of which 15,000 is paid.</summary>
    private int StudentWithFee(string name = "Aman")
    {
        HostelService.Save(new Hostel { HostelId = HostelId, HostelName = "Shri Balaji Hostel", Phone = "98290 12345" });
        int id = AddStudentWithParents(name);
        Invoice invoice = CheckInWithFee(id, AddRoom($"{100 + Count("Room") + 1}"), 50_000m, 12_000m);
        Pay(invoice.InvoiceId, 15_000m, YearStart);
        return id;
    }

    private StudentDue DueOf(int studentId) => PendingDuesService.GetDues(HostelId).Single(d => d.StudentId == studentId);

    [Theory]
    [InlineData("98765 43210", "919876543210")]
    [InlineData("+91 98765-43210", "919876543210")]
    [InlineData("09876543210", "919876543210")]
    [InlineData("919876543210", "919876543210")]
    [InlineData("+44 7700 900123", "447700900123")]
    [InlineData("12345", null)]
    [InlineData("5876543210", null)]
    [InlineData("", null)]
    public void MobileNumbers_GetTheCountryCode(string mobile, string? expected) =>
        Assert.Equal(expected, WhatsAppService.ToWhatsAppNumber(mobile));

    [Fact]
    public void Settings_TokenIsStoredEncrypted_KeptWhenLeftEmpty_AndChecked()
    {
        SetUpWhatsApp();

        string stored = AppSettingRepository.GetAll()["WhatsApp.AccessToken"];
        Assert.StartsWith("dpapi:", stored);
        Assert.DoesNotContain(Token, stored);
        Assert.Equal(Token, WhatsAppSettingsService.Get().AccessToken);

        WhatsAppSettingsService.Save(new WhatsAppSettings
        {
            PhoneNumberId = "1413266881875222", AccessToken = "", FeeTemplate = "Fee_Reminder", AttendanceTemplate = "attendance_alert", Language = "en",
        });
        WhatsAppSettings settings = WhatsAppSettingsService.Get();
        Assert.Equal(Token, settings.AccessToken);
        Assert.Equal("fee_reminder", settings.FeeTemplate);

        Assert.Contains("Phone Number ID", Assert.Throws<ValidationException>(() => WhatsAppSettingsService.Save(
            new WhatsAppSettings { PhoneNumberId = "abc", AccessToken = Token })).Message);
        Assert.Contains("template name", Assert.Throws<ValidationException>(() => WhatsAppSettingsService.Save(
            new WhatsAppSettings { PhoneNumberId = "1", AccessToken = Token, FeeTemplate = "fee reminder" })).Message);
    }

    [Fact]
    public void Settings_PaymentQrIsCopiedIntoTheDataFolder_AndCanBeRemoved()
    {
        WhatsAppSettings settings = SetUpWhatsApp(CreateQr());
        string? qr = WhatsAppSettingsService.PaymentQrFile(settings);
        Assert.NotNull(qr);
        Assert.StartsWith(Path.Combine(DataFolder, "Documents", "WhatsApp"), qr);

        WhatsAppSettingsService.Save(settings, removeQr: true);
        Assert.Null(WhatsAppSettingsService.PaymentQrFile(WhatsAppSettingsService.Get()));
        Assert.False(File.Exists(qr));
    }

    [Fact]
    public void NotSetUp_TellsTheAdminToOpenTheSettings()
    {
        int aman = StudentWithFee();
        Assert.Contains("not set up", Assert.Throws<ValidationException>(() =>
            WhatsAppService.PrepareFeeReminders([DueOf(aman)])).Message);
    }

    [Fact]
    public void FeeReminder_GoesToTheFather_WithTheTemplateValuesInOrder_AndIsKeptInTheHistory()
    {
        SetUpWhatsApp();
        int aman = StudentWithFee();

        (List<WhatsAppMessage> messages, List<string> problems) = WhatsAppService.PrepareFeeReminders([DueOf(aman)]);

        Assert.Empty(problems);
        WhatsAppMessage message = Assert.Single(messages);
        Assert.Equal("919812345678", message.Mobile);
        Assert.Equal("Father", message.Relation);
        Assert.Equal(
            ["Rakesh Aman", "Shri Balaji Hostel", "Aman", AcademicYear.Label(AcademicYear.Current), "62,000", "15,000", "47,000", "98290 12345"],
            message.Parameters);
        Assert.False(message.WithPaymentQr);
        Assert.Contains("Pending: ₹47,000", message.Preview);

        WhatsAppSettings settings = WhatsAppService.GetConfiguredSettings();
        Assert.Null(WhatsAppService.Send(settings, message, qrMediaId: null));
        WhatsAppService.Log(message, null);

        var sent = Assert.Single(_whatsApp.Sent);
        Assert.Equal(("919812345678", "fee_reminder", "en"), (sent.Mobile, sent.Template, sent.Language));
        Assert.Null(sent.Header);
        EmailHistoryEntry history = Assert.Single(EmailService.GetHistory());
        Assert.Equal(EmailType.WhatsAppReminder, history.EmailType);
        Assert.Equal("+919812345678", history.RecipientEmail);
        Assert.Equal(EmailStatus.Sent, history.Status);
        Assert.Equal(DateTime.Today, DueOf(aman).LastReminderDate?.Date);
    }

    [Fact]
    public void FeeReminder_WithPaymentQr_UploadsTheImageOnceAndSendsItAsTheHeader()
    {
        WhatsAppSettings settings = SetUpWhatsApp(CreateQr());
        int aman = StudentWithFee("Aman");
        int ravi = StudentWithFee("Ravi");

        (List<WhatsAppMessage> messages, _) = WhatsAppService.PrepareFeeReminders([DueOf(aman), DueOf(ravi)]);
        string? media = WhatsAppService.UploadPaymentQr(settings);
        foreach (WhatsAppMessage message in messages)
        {
            Assert.True(message.WithPaymentQr);
            Assert.Null(WhatsAppService.Send(settings, message, media));
        }

        Assert.Single(_whatsApp.Uploaded);
        Assert.All(_whatsApp.Sent, s => Assert.Equal("media-1", s.Header));
        Assert.Equal(2, _whatsApp.Sent.Count);
    }

    [Fact]
    public void FeeReminder_FallsBackToTheMother_AndStudentsWithoutAMobileAreListed()
    {
        SetUpWhatsApp();
        int aman = StudentWithFee("Aman");
        int ravi = StudentWithFee("Ravi");
        Student amanStudent = StudentService.GetStudent(aman)!;
        amanStudent.FatherMobile = "";
        StudentService.Save(amanStudent);
        Student raviStudent = StudentService.GetStudent(ravi)!;
        raviStudent.FatherMobile = raviStudent.MotherMobile = "";
        StudentService.Save(raviStudent);

        (List<WhatsAppMessage> messages, List<string> problems) = WhatsAppService.PrepareFeeReminders([DueOf(aman), DueOf(ravi)]);

        WhatsAppMessage message = Assert.Single(messages);
        Assert.Equal(("919812345679", "Mother", "Sunita Aman"), (message.Mobile, message.Relation, message.RecipientName));
        Assert.Contains("Ravi", Assert.Single(problems));
    }

    [Fact]
    public void Attendance_GoesToTheMother_OncePerDate()
    {
        SetUpWhatsApp();
        HostelService.Save(new Hostel { HostelId = HostelId, HostelName = "Shri Balaji Hostel", Phone = "98290 12345" });
        int aman = AddStudentWithParents("Aman", admission: DateTime.Today.AddDays(-5));
        AllocationService.CheckIn(aman, AddRoom("101").RoomId, DateTime.Today.AddDays(-5));
        AttendanceSheet sheet = AttendanceService.GetSheet(HostelId, DateTime.Today);
        sheet.Entries.Single(e => e.StudentId == aman).IsPresent = false;
        AttendanceService.Save(HostelId, DateTime.Today, sheet.Entries);

        (List<WhatsAppMessage> messages, List<string> problems) = WhatsAppService.PrepareAbsences(HostelId, DateTime.Today);

        Assert.Empty(problems);
        WhatsAppMessage message = Assert.Single(messages);
        Assert.Equal(("919812345679", "attendance_alert"), (message.Mobile, message.TemplateName));
        string date = DateTime.Today.ToString("dd MMM yyyy", System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(["Sunita Aman", "Aman", "Shri Balaji Hostel", date, "98290 12345"], message.Parameters);

        Assert.Null(WhatsAppService.Send(WhatsAppService.GetConfiguredSettings(), message, null));
        WhatsAppService.Log(message, null);

        Assert.Empty(WhatsAppService.PrepareAbsences(HostelId, DateTime.Today).Messages);
        Assert.Single(WhatsAppService.PrepareAbsences(HostelId, DateTime.Today, includeAlreadySent: true).Messages);
    }

    [Fact]
    public void Errors_AreExplained_AndAFailedReminderIsNotCountedAsSent()
    {
        SetUpWhatsApp();
        int aman = StudentWithFee();
        _whatsApp.FailFor["919812345678"] = new WhatsAppApiException(131026, "Message undeliverable");
        WhatsAppMessage message = Assert.Single(WhatsAppService.PrepareFeeReminders([DueOf(aman)]).Messages);

        string? error = WhatsAppService.Send(WhatsAppService.GetConfiguredSettings(), message, null);
        WhatsAppService.Log(message, error);

        Assert.Contains("cannot receive WhatsApp", error);
        Assert.Equal(EmailStatus.Failed, Assert.Single(EmailService.GetHistory()).Status);
        Assert.Null(DueOf(aman).LastReminderDate);
        Assert.Contains("access token", WhatsAppService.FriendlyError(new WhatsAppApiException(190, "Invalid OAuth access token")));
        Assert.Contains("template was not found", WhatsAppService.FriendlyError(new WhatsAppApiException(132001, "Template name does not exist")));
        Assert.Contains("internet", WhatsAppService.FriendlyError(new HttpRequestException("No such host")));
    }

    [Fact]
    public void TestMessage_SendsMetasHelloWorldTemplate()
    {
        WhatsAppSettings settings = SetUpWhatsApp();

        Assert.Contains("valid mobile", WhatsAppService.SendTest(settings, "123"));
        Assert.Null(WhatsAppService.SendTest(settings, "98765 43210"));

        var sent = Assert.Single(_whatsApp.Sent);
        Assert.Equal(("919876543210", "hello_world", "en_US", 0), (sent.Mobile, sent.Template, sent.Language, sent.Parameters.Count));
    }

    [Fact]
    public void CloudApiPayload_HasTheHeaderImageAndTheBodyValuesInOrder()
    {
        JsonNode payload = JsonNode.Parse(CloudApiWhatsAppSender.BuildTemplatePayload(
            "919812345678", "fee_reminder", "en", ["Rakesh", "Shri Balaji Hostel"], "media-7"))!;

        Assert.Equal("whatsapp", payload["messaging_product"]!.GetValue<string>());
        Assert.Equal("919812345678", payload["to"]!.GetValue<string>());
        Assert.Equal("template", payload["type"]!.GetValue<string>());
        Assert.Equal("fee_reminder", payload["template"]!["name"]!.GetValue<string>());
        Assert.Equal("en", payload["template"]!["language"]!["code"]!.GetValue<string>());
        JsonArray components = payload["template"]!["components"]!.AsArray();
        Assert.Equal("header", components[0]!["type"]!.GetValue<string>());
        Assert.Equal("media-7", components[0]!["parameters"]![0]!["image"]!["id"]!.GetValue<string>());
        Assert.Equal("body", components[1]!["type"]!.GetValue<string>());
        Assert.Equal(["Rakesh", "Shri Balaji Hostel"],
            components[1]!["parameters"]!.AsArray().Select(p => p!["text"]!.GetValue<string>()));

        JsonNode plain = JsonNode.Parse(CloudApiWhatsAppSender.BuildTemplatePayload("919812345678", "hello_world", "en_US", [], null))!;
        Assert.Empty(plain["template"]!["components"]!.AsArray());
    }
}
