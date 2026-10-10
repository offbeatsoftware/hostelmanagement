using System.Data.OleDb;
using System.Globalization;
using HostelManagement.Data;
using HostelManagement.Models;
using HostelManagement.Utilities;

namespace HostelManagement.Services;

/// <summary>Everything printed on an invoice: the fee for the year and every payment made against it.</summary>
public sealed record InvoicePrintData(Invoice Invoice, Hostel Hostel, Student Student, string RoomText, List<Payment> Payments);

/// <summary>The room rent and transport agreed with a student for one academic year.</summary>
public sealed record YearFee(decimal RoomRent, decimal TransportAmount, string? Remarks = null)
{
    public decimal Total => RoomRent + TransportAmount;
}

/// <summary>A student without an invoice for the academic year, offered on the New Year Fees screen.</summary>
public sealed record NewYearFeeCandidate(Student Student, string RoomText, Invoice? LastYear);

/// <summary>
/// Yearly fees (client decisions, version 1.2): the rent is agreed per student, not per room type.
/// When a student is put in a room, the admin enters the room rent and the transport amount for the
/// academic year (July to June); the full amount is due even when the student joins mid-year. Every new
/// academic year the admin enters a new fee. The student pays in any number of payments; the balance is the
/// total minus everything paid. The admin can change the amounts later, but not below what is already paid.
/// Invoice numbers: SBH/2026-27/0001, one sequence per academic year. No GST, deposits or discounts.
/// </summary>
public static class InvoiceService
{
    public const string NumberPrefix = "SBH";

    /// <summary>The highest yearly rent or transport amount accepted (catches typing mistakes).</summary>
    public const decimal MaxAmount = 10_000_000m;

    public static List<Invoice> GetInvoices(int hostelId) => InvoiceRepository.GetForHostel(hostelId);

    public static Invoice? GetInvoice(int invoiceId) => InvoiceRepository.Get(invoiceId);

    public static List<Invoice> GetInvoicesForStudent(int studentId) => InvoiceRepository.GetForStudent(studentId);

    /// <summary>The student's invoice for the academic year, if any.</summary>
    public static Invoice? GetForYear(int studentId, int academicYear) => InvoiceRepository.GetForYear(studentId, academicYear);

    /// <summary>"2026-27" for a date in the academic year July 2026 to June 2027.</summary>
    public static string AcademicYearLabel(DateTime date) => AcademicYear.Label(AcademicYear.Of(date));

    /// <summary>Checks the amounts of a yearly fee.</summary>
    public static void ValidateFee(YearFee fee)
    {
        CheckAmount(fee.RoomRent, "room rent");
        if (fee.RoomRent <= 0)
        {
            throw new ValidationException("Please enter the room rent for the year.");
        }
        CheckAmount(fee.TransportAmount, "transport amount");
        Validators.CheckLength(Validators.Clean(fee.Remarks), 255, "Remarks");
    }

    /// <summary>Creates the student's invoice for an academic year with the agreed rent and transport.</summary>
    public static Invoice Create(int studentId, int academicYear, YearFee fee, DateTime? invoiceDate = null)
    {
        Student student = StudentService.GetStudent(studentId)
            ?? throw new ValidationException("Please select the student.");
        int invoiceId = Db.InTransaction((connection, transaction) =>
            Insert(connection, transaction, student, academicYear, fee, invoiceDate));
        return InvoiceRepository.Get(invoiceId)!;
    }

    /// <summary>Creates the invoice inside an existing transaction (used by check-in).</summary>
    internal static int Insert(OleDbConnection connection, OleDbTransaction transaction, Student student, int academicYear,
        YearFee fee, DateTime? invoiceDate = null)
    {
        ValidateFee(fee);
        DateTime date = (invoiceDate ?? DateTime.Today).Date;
        if (date > DateTime.Today)
        {
            throw new ValidationException("The invoice date cannot be in the future.");
        }
        if (academicYear < 2000 || academicYear > AcademicYear.Current + 1)
        {
            throw new ValidationException("Please choose this academic year or the next one.");
        }
        if (InvoiceRepository.Exists(connection, transaction, student.StudentId, academicYear))
        {
            throw new ValidationException(
                $"{student.StudentName} already has a fee for {AcademicYear.Label(academicYear)}. Use Edit Fee to change it.");
        }

        string prefix = $"{NumberPrefix}/{AcademicYear.Label(academicYear)}/";
        var invoice = new Invoice
        {
            StudentId = student.StudentId,
            InvoiceDate = date,
            AcademicYear = academicYear,
            RoomRent = fee.RoomRent,
            TransportAmount = fee.TransportAmount,
            Remarks = Validators.Clean(fee.Remarks),
            InvoiceNumber = prefix + NextSequence(InvoiceRepository.GetNumbersStartingWith(connection, transaction, prefix), prefix)
                .ToString("0000", CultureInfo.InvariantCulture),
        };
        int invoiceId = InvoiceRepository.Insert(connection, transaction, invoice);
        AppLogger.Info($"Created invoice {invoice.InvoiceNumber} for student {student.StudentId}: " +
            $"rent {fee.RoomRent}, transport {fee.TransportAmount}.");
        return invoiceId;
    }

    /// <summary>Changes the agreed rent and transport of an invoice. The total cannot go below the amount already paid.</summary>
    public static Invoice UpdateFee(int invoiceId, YearFee fee)
    {
        Invoice invoice = InvoiceRepository.Get(invoiceId)
            ?? throw new ValidationException("This invoice no longer exists.");
        ValidateFee(fee);
        if (fee.Total < invoice.PaidAmount)
        {
            throw new ValidationException(
                $"{Money.Format(invoice.PaidAmount)} has already been paid against invoice {invoice.InvoiceNumber}. " +
                "The new total cannot be less than that.");
        }

        InvoiceRepository.UpdateFee(invoiceId, fee.RoomRent, fee.TransportAmount, Validators.Clean(fee.Remarks));
        AppLogger.Info($"Changed invoice {invoice.InvoiceNumber}: rent {invoice.RoomRent} to {fee.RoomRent}, " +
            $"transport {invoice.TransportAmount} to {fee.TransportAmount}.");
        return InvoiceRepository.Get(invoiceId)!;
    }

    /// <summary>
    /// Students of the hostel who are in a room and have no fee for the academic year, with last year's
    /// invoice for reference (the admin enters the new amounts; client decision).
    /// </summary>
    public static List<NewYearFeeCandidate> GetNewYearCandidates(int hostelId, int academicYear)
    {
        HashSet<int> invoiced = InvoiceRepository.GetForHostel(hostelId)
            .Where(i => i.AcademicYear == academicYear)
            .Select(i => i.StudentId)
            .ToHashSet();
        Dictionary<int, Invoice> lastYear = InvoiceRepository.GetForHostel(hostelId)
            .Where(i => i.AcademicYear == academicYear - 1)
            .GroupBy(i => i.StudentId)
            .ToDictionary(g => g.Key, g => g.First());

        return AllocationService.GetAllocations(hostelId)
            .Where(a => !invoiced.Contains(a.StudentId))
            .Select(a => new NewYearFeeCandidate(StudentService.GetStudent(a.StudentId)!, a.RoomAndBed,
                lastYear.GetValueOrDefault(a.StudentId)))
            .OrderBy(c => c.Student.StudentName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>Creates the fees entered on the New Year Fees screen. Every fee is checked before any is saved.</summary>
    public static List<Invoice> CreateForYear(int academicYear, IReadOnlyDictionary<int, YearFee> fees)
    {
        if (fees.Count == 0)
        {
            throw new ValidationException("Please enter the room rent for at least one student.");
        }
        var students = new List<Student>();
        foreach ((int studentId, YearFee fee) in fees)
        {
            Student student = StudentService.GetStudent(studentId)
                ?? throw new ValidationException("A student in the list no longer exists. Please refresh.");
            try
            {
                ValidateFee(fee);
            }
            catch (ValidationException ex)
            {
                throw new ValidationException($"{student.StudentName}: {ex.Message}");
            }
            students.Add(student);
        }

        List<int> ids = Db.InTransaction((connection, transaction) =>
            students.Select(s => Insert(connection, transaction, s, academicYear, fees[s.StudentId])).ToList());
        return ids.Select(id => InvoiceRepository.Get(id)!).ToList();
    }

    /// <summary>Deletes an invoice that has no payments or emails (for example one created by mistake).</summary>
    public static void Delete(int invoiceId)
    {
        Invoice invoice = InvoiceRepository.Get(invoiceId)
            ?? throw new ValidationException("This invoice no longer exists.");
        if (InvoiceRepository.CountReferences(invoiceId) > 0)
        {
            throw new ValidationException(
                $"Invoice {invoice.InvoiceNumber} has payments or emails and cannot be deleted. Use Edit Fee to change it.");
        }

        Db.InTransaction((connection, transaction) => InvoiceRepository.Delete(connection, transaction, invoiceId));
        AppLogger.Info($"Deleted invoice {invoice.InvoiceNumber}.");
    }

    /// <summary>Hostel, student, room and payment details for printing the invoice.</summary>
    public static InvoicePrintData GetPrintData(int invoiceId)
    {
        Invoice invoice = InvoiceRepository.Get(invoiceId)
            ?? throw new ValidationException("This invoice no longer exists.");
        Student student = StudentService.GetStudent(invoice.StudentId)
            ?? throw new ValidationException("The invoice's student no longer exists.");
        int hostelId = CollegeRepository.Get(student.CollegeId)?.HostelId ?? 0;
        Hostel hostel = HostelService.GetHostel(hostelId)
            ?? throw new ValidationException("The invoice's hostel no longer exists.");

        // The room the student is in now, or the last room during the academic year.
        RoomAllocation? allocation = AllocationRepository.GetForStudent(student.StudentId)
            .Where(a => a.CheckInDate.Date <= AcademicYear.End(invoice.AcademicYear)
                && (a.CheckOutDate ?? DateTime.MaxValue).Date >= AcademicYear.Start(invoice.AcademicYear))
            .OrderByDescending(a => a.CheckInDate)
            .ThenByDescending(a => a.AllocationId)
            .FirstOrDefault();

        List<Payment> payments = PaymentRepository.GetForInvoice(invoiceId)
            .OrderBy(p => p.PaymentDate)
            .ThenBy(p => p.PaymentId)
            .ToList();

        return new InvoicePrintData(invoice, hostel, student,
            allocation is null ? string.Empty : $"Room {allocation.RoomAndBed}", payments);
    }

    private static void CheckAmount(decimal amount, string name)
    {
        if (amount < 0)
        {
            throw new ValidationException($"The {name} cannot be negative.");
        }
        if (amount > MaxAmount)
        {
            throw new ValidationException($"The {name} cannot be more than {Money.Format(MaxAmount)}.");
        }
        if (decimal.Round(amount, 2) != amount)
        {
            throw new ValidationException($"The {name} can have at most two decimal places (paise).");
        }
    }

    private static int NextSequence(IEnumerable<string> numbers, string prefix) =>
        numbers
            .Select(n => int.TryParse(n[prefix.Length..], NumberStyles.None, CultureInfo.InvariantCulture, out int value) ? value : 0)
            .DefaultIfEmpty(0)
            .Max() + 1;
}
