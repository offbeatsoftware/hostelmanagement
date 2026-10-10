using System.Globalization;
using HostelManagement.Models;
using HostelManagement.Utilities;

namespace HostelManagement.Services;

/// <summary>
/// Reports of the selected hostel: student list, room occupancy, payments received between two dates with
/// daily and method totals, the yearly fees (invoices) of an academic year and the pending fees.
/// Each report is a <see cref="ReportTable"/> that the screen shows and exports to PDF and Excel.
/// </summary>
public static class ReportService
{
    public const string AllOption = "All";

    public static ReportTable StudentList(Hostel hostel, string? status = null, int? collegeId = null)
    {
        List<Student> students = StudentService.GetStudents(hostel.HostelId)
            .Where(s => status is null || s.Status == status)
            .Where(s => collegeId is null || s.CollegeId == collegeId)
            .ToList();

        var filters = new List<string> { status is null ? "All students" : $"{status} students" };
        if (collegeId is not null)
        {
            filters.Add(CollegeService.Search(hostel.HostelId).FirstOrDefault(c => c.CollegeId == collegeId)?.CollegeName ?? "");
        }

        var rows = students.Select(s => new ReportRow(
        [
            s.StudentName, s.CollegeName, Join(s.Course, s.ClassName), s.RoomNumber, s.Mobile, s.AadhaarMasked,
            s.FatherName, s.FatherMobile, s.AdmissionDate, s.Status,
        ])).ToList();
        rows.Add(new ReportRow([$"{students.Count} students", null, null, null, null, null, null, null, null, null], ReportRowStyle.Total));

        return new ReportTable
        {
            Title = "Student List",
            Subtitles = Subtitles(hostel, string.Join(", ", filters.Where(f => f.Length > 0))),
            Columns =
            [
                new("Student", Width: 1.6), new("College", Width: 1.5), new("Course / class", Width: 1.2), new("Room", Width: 0.5),
                new("Mobile", Width: 0.9), new("Aadhaar", Width: 1), new("Father", Width: 1.3), new("Father mobile", Width: 0.9),
                new("Admission", ReportValueKind.Date, 0.8), new("Status", Width: 0.6),
            ],
            Rows = rows,
            FileName = $"Students_{SafeName(hostel.HostelName)}_{DateTime.Today:yyyy-MM-dd}",
        };
    }

    public static ReportTable RoomOccupancy(Hostel hostel)
    {
        List<Room> rooms = RoomService.GetRooms(hostel.HostelId);
        Dictionary<int, List<string>> studentsByRoom = AllocationService.GetAllocations(hostel.HostelId)
            .GroupBy(a => a.RoomId)
            .ToDictionary(g => g.Key, g => g.Select(a => a.StudentName).OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase).ToList());

        var rows = rooms.Select(r => new ReportRow(
        [
            r.RoomNumber, r.Floor, r.RoomFor, r.SharingName, r.Capacity, r.Occupied, r.Available,
            string.Join(", ", studentsByRoom.GetValueOrDefault(r.RoomId, [])), r.Status,
        ])).ToList();
        rows.Add(new ReportRow(
        [
            $"{rooms.Count} rooms", null, null, null, rooms.Where(r => r.IsActive).Sum(r => r.Capacity), rooms.Sum(r => r.Occupied),
            rooms.Sum(r => r.Available), null, null,
        ], ReportRowStyle.Total));

        return new ReportTable
        {
            Title = "Room Occupancy",
            Subtitles = Subtitles(hostel, "Current students in each room"),
            Columns =
            [
                new("Room", Width: 0.6), new("Floor", Width: 0.5), new("For", Width: 0.5), new("Sharing", Width: 0.7),
                new("Beds", ReportValueKind.Number, 0.5), new("Occupied", ReportValueKind.Number, 0.6), new("Free", ReportValueKind.Number, 0.5),
                new("Students", Width: 4.2), new("Status", Width: 0.6),
            ],
            Rows = rows,
            FileName = $"Rooms_{SafeName(hostel.HostelName)}_{DateTime.Today:yyyy-MM-dd}",
        };
    }

    /// <summary>Payments received between two dates (inclusive), with a total after each day, then per method.</summary>
    public static ReportTable PaymentsReceived(Hostel hostel, DateTime from, DateTime to, string? method = null)
    {
        CheckDates(from, to);
        List<Payment> payments = PaymentService.GetPayments(hostel.HostelId)
            .Where(p => p.PaymentDate >= from.Date && p.PaymentDate <= to.Date)
            .Where(p => method is null || p.PaymentMethod == method)
            .OrderBy(p => p.PaymentDate)
            .ThenBy(p => p.PaymentId)
            .ToList();

        var rows = new List<ReportRow>();
        foreach (IGrouping<DateTime, Payment> day in payments.GroupBy(p => p.PaymentDate.Date))
        {
            rows.AddRange(day.Select(p => new ReportRow(
                [p.PaymentDate, p.ReceiptNumber, p.StudentName, p.InvoiceNumber, p.PaymentMethod, p.Reference, p.Amount])));
            rows.Add(new ReportRow(
                [null, $"Total for {Date(day.Key)} ({PaymentCount(day.Count())})", null, null, null, null, day.Sum(p => p.Amount)],
                ReportRowStyle.Subtotal));
        }
        foreach (IGrouping<string, Payment> byMethod in payments.GroupBy(p => p.PaymentMethod).OrderBy(g => PaymentMethodOrder(g.Key)))
        {
            rows.Add(new ReportRow(
                [null, $"{byMethod.Key} total ({PaymentCount(byMethod.Count())})", null, null, null, null, byMethod.Sum(p => p.Amount)],
                ReportRowStyle.Subtotal));
        }
        rows.Add(new ReportRow(
            [null, $"Total received ({PaymentCount(payments.Count)})", null, null, null, null, payments.Sum(p => p.Amount)],
            ReportRowStyle.Total));

        return new ReportTable
        {
            Title = "Payments Received",
            Subtitles = Subtitles(hostel, $"{Date(from)} to {Date(to)}, {(method is null ? "all payment methods" : method)}"),
            Columns =
            [
                new("Date", ReportValueKind.Date, 0.8), new("Receipt no.", Width: 1.4), new("Student", Width: 1.5),
                new("Invoice no.", Width: 1.2), new("Paid by", Width: 0.8), new("Reference", Width: 1.1), new("Amount", ReportValueKind.Money, 1),
            ],
            Rows = rows,
            FileName = $"Payments_{SafeName(hostel.HostelName)}_{from:yyyy-MM-dd}_to_{to:yyyy-MM-dd}",
        };
    }

    /// <summary>The yearly fees (invoices) of an academic year, or of all years, optionally only Unpaid, Partly paid or Paid.</summary>
    public static ReportTable Invoices(Hostel hostel, int? academicYear, string? status = null)
    {
        List<Invoice> invoices = InvoiceService.GetInvoices(hostel.HostelId)
            .Where(i => academicYear is null || i.AcademicYear == academicYear)
            .Where(i => status is null || i.Status == status)
            .OrderBy(i => i.AcademicYear)
            .ThenBy(i => i.StudentName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var rows = invoices.Select(i => new ReportRow(
        [
            i.InvoiceNumber, i.YearText, i.StudentName, i.RoomRent, i.TransportAmount, i.TotalAmount, i.PaidAmount, i.PendingAmount, i.Status,
        ])).ToList();
        rows.Add(new ReportRow(
        [
            $"{invoices.Count} invoices", null, null, invoices.Sum(i => i.RoomRent), invoices.Sum(i => i.TransportAmount),
            invoices.Sum(i => i.TotalAmount), invoices.Sum(i => i.PaidAmount), invoices.Sum(i => i.PendingAmount), null,
        ], ReportRowStyle.Total));

        string yearText = academicYear is int year ? $"Academic year {AcademicYear.Label(year)}" : "All academic years";
        return new ReportTable
        {
            Title = "Fees (Invoices)",
            Subtitles = Subtitles(hostel, $"{yearText}, {(status is null ? "all invoices" : status.ToLowerInvariant())}"),
            Columns =
            [
                new("Invoice no.", Width: 1.3), new("Year", Width: 0.6), new("Student", Width: 1.6),
                new("Room rent", ReportValueKind.Money, 1), new("Transport", ReportValueKind.Money, 1), new("Total fee", ReportValueKind.Money, 1),
                new("Paid", ReportValueKind.Money, 1), new("Pending", ReportValueKind.Money, 1), new("Status", Width: 0.8),
            ],
            Rows = rows,
            FileName = $"Fees_{SafeName(hostel.HostelName)}_{(academicYear is int y ? AcademicYear.Label(y) : "all-years")}",
        };
    }

    /// <summary>Pending fees: a bold line per student with total, paid and pending, then that student's invoices.</summary>
    public static ReportTable PendingDues(Hostel hostel, DateTime asOf)
    {
        List<StudentDue> dues = PendingDuesService.GetDues(hostel.HostelId);

        var rows = new List<ReportRow>();
        foreach (StudentDue due in dues)
        {
            string name = due.StudentStatus == StudentStatus.Left ? $"{due.StudentName} (left)" : due.StudentName;
            rows.Add(new ReportRow(
                [name, due.RoomNumber, due.FatherText, null, due.TotalAmount, due.PaidAmount, due.PendingAmount],
                ReportRowStyle.Group));
            rows.AddRange(due.Invoices.Select(i => new ReportRow(
                [$"    {i.InvoiceNumber}", null, null, i.YearText, i.TotalAmount, i.PaidAmount, i.PendingAmount])));
        }
        rows.Add(new ReportRow(
            [$"{dues.Count} students", null, null, null, dues.Sum(d => d.TotalAmount), dues.Sum(d => d.PaidAmount), dues.Sum(d => d.PendingAmount)],
            ReportRowStyle.Total));

        return new ReportTable
        {
            Title = "Pending Fees",
            Subtitles = Subtitles(hostel, $"As on {Date(asOf)}, all students with an amount pending"),
            Columns =
            [
                new("Student / invoice", Width: 2.2), new("Room", Width: 0.5), new("Father / mobile", Width: 1.8), new("Year", Width: 0.6),
                new("Total fee", ReportValueKind.Money, 1), new("Paid", ReportValueKind.Money, 1), new("Pending", ReportValueKind.Money, 1),
            ],
            Rows = rows,
            FileName = $"PendingFees_{SafeName(hostel.HostelName)}_{asOf:yyyy-MM-dd}",
        };
    }

    /// <summary>A value as shown on screen and in the PDF.</summary>
    public static string FormatValue(object? value, ReportValueKind kind, bool pdf = false) => value switch
    {
        null => string.Empty,
        DateTime date => Date(date),
        decimal amount => pdf ? "Rs. " + amount.ToString("N2", Money.Culture) : Money.Format(amount),
        int number => number.ToString(CultureInfo.InvariantCulture),
        _ => Convert.ToString(value, CultureInfo.CurrentCulture) ?? string.Empty,
    };

    /// <summary>Letters and digits only, for file names: "Shri Balaji Hostel" → "Shri-Balaji-Hostel".</summary>
    public static string SafeName(string text)
    {
        string name = string.Concat(text.Select(c => char.IsLetterOrDigit(c) ? c : '-')).Trim('-');
        while (name.Contains("--", StringComparison.Ordinal))
        {
            name = name.Replace("--", "-", StringComparison.Ordinal);
        }
        return name.Length > 0 ? name : "Hostel";
    }

    private static List<string> Subtitles(Hostel hostel, string filter) =>
        [hostel.HostelName, filter, $"Printed on {DateTime.Now.ToString("dd MMM yyyy HH:mm", CultureInfo.InvariantCulture)}"];

    private static void CheckDates(DateTime from, DateTime to)
    {
        if (from.Date > to.Date)
        {
            throw new ValidationException("The From date must be on or before the To date.");
        }
    }

    private static int PaymentMethodOrder(string method)
    {
        int index = PaymentMethod.All.ToList().IndexOf(method);
        return index < 0 ? int.MaxValue : index;
    }

    private static string Join(string first, string second) =>
        first.Length > 0 && second.Length > 0 ? $"{first}, {second}" : first + second;

    private static string PaymentCount(int count) => count == 1 ? "1 payment" : $"{count} payments";

    private static string Date(DateTime date) => date.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);
}
