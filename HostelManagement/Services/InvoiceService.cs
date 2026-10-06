using System.Globalization;
using HostelManagement.Data;
using HostelManagement.Models;
using HostelManagement.Utilities;

namespace HostelManagement.Services;

/// <summary>Everything printed on an invoice.</summary>
public sealed record InvoicePrintData(Invoice Invoice, Hostel Hostel, Student Student, Parent? Parent, string RoomText);

/// <summary>
/// Invoices (client decisions, Phase 8):
/// one invoice per student per billing period of the hostel (twice or four times a year, academic year
/// from July); rent is the full installment of the room's yearly rent even when the student joins
/// mid-period; extra services are charged per month, a part month counting as a full month; services
/// included in the rent are listed at no charge; no GST, deposits or discounts.
/// Invoice numbers: SBH/2026-27/0001, one sequence per academic year.
/// </summary>
public static class InvoiceService
{
    public const string NumberPrefix = "SBH";

    public static List<Invoice> GetInvoices(int hostelId) => InvoiceRepository.GetForHostel(hostelId);

    public static Invoice? GetInvoice(int invoiceId) => InvoiceRepository.Get(invoiceId);

    /// <summary>Billing periods offered when creating an invoice: last, this and next academic year.</summary>
    public static List<BillingPeriod> GetBillingPeriods(string frequency, DateTime today) =>
        new[] { today.AddYears(-1), today, today.AddYears(1) }
            .SelectMany(date => BillingPeriods.ForAcademicYear(date, frequency))
            .ToList();

    /// <summary>"2026-27" for a date in the academic year July 2026 to June 2027.</summary>
    public static string AcademicYearLabel(DateTime date)
    {
        int startYear = date.Month >= BillingPeriods.AcademicYearStartMonth ? date.Year : date.Year - 1;
        return $"{startYear}-{(startYear + 1) % 100:00}";
    }

    /// <summary>Calculates the invoice for a student and billing period without saving it.</summary>
    public static Invoice Preview(int studentId, BillingPeriod period, DateTime? invoiceDate = null)
    {
        Student student = StudentService.GetStudent(studentId)
            ?? throw new ValidationException("Please select the student.");

        RoomAllocation allocation = AllocationRepository.GetForStudent(studentId)
            .Where(a => a.CheckInDate.Date <= period.To && (a.CheckOutDate ?? DateTime.MaxValue).Date >= period.From)
            .OrderByDescending(a => a.CheckInDate)
            .ThenByDescending(a => a.AllocationId)
            .FirstOrDefault()
            ?? throw new ValidationException(
                $"{student.StudentName} was not in a room during {period.Name}. Only students in a room can be invoiced.");

        Room room = RoomRepository.Get(allocation.RoomId)
            ?? throw new ValidationException("The student's room no longer exists.");
        Hostel hostel = HostelService.GetHostel(room.HostelId)
            ?? throw new ValidationException("The student's hostel no longer exists.");

        if (BillingPeriods.For(period.From, hostel.BillingFrequency) != period)
        {
            throw new ValidationException(
                $"{period.Name} is not a billing period of {hostel.HostelName} " +
                $"(billed {BillingFrequency.DisplayName(hostel.BillingFrequency).ToLowerInvariant()}).");
        }
        if (room.Rent <= 0)
        {
            throw new ValidationException(
                $"The yearly rent for {room.SharingName} sharing is not set. Enter it on the Rooms screen first.");
        }
        if (InvoiceRepository.Exists(studentId, period.From))
        {
            throw new ValidationException($"{student.StudentName} already has an invoice for {period.Name}.");
        }

        var invoice = new Invoice
        {
            StudentId = studentId,
            StudentName = student.StudentName,
            InvoiceDate = (invoiceDate ?? DateTime.Today).Date,
            BillingFrom = period.From,
            BillingTo = period.To,
        };

        decimal rent = BillingPeriods.InstallmentAmount(room.Rent, hostel.BillingFrequency);
        invoice.Items.Add(new InvoiceItem
        {
            Description = $"Room rent: room {room.RoomNumber} ({room.SharingName} sharing), {period.Name}",
            Quantity = 1,
            Rate = rent,
            Amount = rent,
        });

        invoice.Items.AddRange(ExtraServiceItems(studentId, period));

        foreach (ServiceItem included in ServiceItemService.GetIncludedServices(hostel.HostelId))
        {
            invoice.Items.Add(new InvoiceItem { Description = $"{included.ServiceName} (included in rent)", Quantity = 1 });
        }

        invoice.TotalAmount = invoice.Items.Sum(i => i.Amount);
        return invoice;
    }

    /// <summary>Calculates and saves the invoice with the next invoice number.</summary>
    public static Invoice Create(int studentId, BillingPeriod period, DateTime? invoiceDate = null)
    {
        Invoice invoice = Preview(studentId, period, invoiceDate);
        if (invoice.InvoiceDate > DateTime.Today)
        {
            throw new ValidationException("The invoice date cannot be in the future.");
        }

        string prefix = $"{NumberPrefix}/{AcademicYearLabel(period.From)}/";
        int invoiceId = Db.InTransaction((connection, transaction) =>
        {
            invoice.InvoiceNumber = prefix + NextSequence(InvoiceRepository.GetNumbersStartingWith(connection, transaction, prefix), prefix)
                .ToString("0000", CultureInfo.InvariantCulture);
            return InvoiceRepository.Insert(connection, transaction, invoice);
        });

        AppLogger.Info($"Created invoice {invoice.InvoiceNumber} for student {studentId}.");
        return InvoiceRepository.Get(invoiceId)!;
    }

    /// <summary>Deletes an invoice that has no payments or emails (for example one created by mistake).</summary>
    public static void Delete(int invoiceId)
    {
        Invoice invoice = InvoiceRepository.Get(invoiceId)
            ?? throw new ValidationException("This invoice no longer exists.");
        if (InvoiceRepository.CountReferences(invoiceId) > 0)
        {
            throw new ValidationException(
                $"Invoice {invoice.InvoiceNumber} has payments or emails and cannot be deleted.");
        }

        Db.InTransaction((connection, transaction) => InvoiceRepository.Delete(connection, transaction, invoiceId));
        AppLogger.Info($"Deleted invoice {invoice.InvoiceNumber}.");
    }

    /// <summary>Hostel, student, parent and room details for printing the invoice.</summary>
    public static InvoicePrintData GetPrintData(int invoiceId)
    {
        Invoice invoice = InvoiceRepository.Get(invoiceId)
            ?? throw new ValidationException("This invoice no longer exists.");
        Student student = StudentService.GetStudent(invoice.StudentId)
            ?? throw new ValidationException("The invoice's student no longer exists.");
        int hostelId = CollegeRepository.Get(student.CollegeId)?.HostelId ?? 0;
        Hostel hostel = HostelService.GetHostel(hostelId)
            ?? throw new ValidationException("The invoice's hostel no longer exists.");

        RoomAllocation? allocation = AllocationRepository.GetForStudent(student.StudentId)
            .Where(a => a.CheckInDate.Date <= invoice.BillingTo && (a.CheckOutDate ?? DateTime.MaxValue).Date >= invoice.BillingFrom)
            .OrderByDescending(a => a.CheckInDate)
            .FirstOrDefault();

        return new InvoicePrintData(invoice, hostel, student, StudentService.GetPrimaryParent(student.StudentId),
            allocation is null ? string.Empty : $"Room {allocation.RoomNumber}");
    }

    /// <summary>Extra services used during the period: months touched, a part month counting as a full month.</summary>
    private static IEnumerable<InvoiceItem> ExtraServiceItems(int studentId, BillingPeriod period)
    {
        foreach (IGrouping<int, StudentServiceUse> uses in StudentServiceRepository.GetForStudent(studentId).GroupBy(u => u.ServiceId))
        {
            ServiceItem? service = ServiceItemRepository.Get(uses.Key);
            if (service is null || service.IsIncludedInRent)
            {
                continue;
            }

            var months = new HashSet<DateTime>();
            foreach (StudentServiceUse use in uses)
            {
                DateTime from = use.StartDate.Date > period.From ? use.StartDate.Date : period.From;
                DateTime to = (use.EndDate ?? DateTime.MaxValue).Date < period.To ? use.EndDate!.Value.Date : period.To;
                for (var month = new DateTime(from.Year, from.Month, 1); month <= to; month = month.AddMonths(1))
                {
                    months.Add(month);
                }
            }

            if (months.Count > 0)
            {
                yield return new InvoiceItem
                {
                    Description = $"{service.ServiceName}: {months.Count} month{(months.Count == 1 ? "" : "s")}",
                    Quantity = months.Count,
                    Rate = service.MonthlyRate,
                    Amount = months.Count * service.MonthlyRate,
                };
            }
        }
    }

    private static int NextSequence(IEnumerable<string> numbers, string prefix) =>
        numbers
            .Select(n => int.TryParse(n[prefix.Length..], NumberStyles.None, CultureInfo.InvariantCulture, out int value) ? value : 0)
            .DefaultIfEmpty(0)
            .Max() + 1;
}
