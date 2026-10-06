using HostelManagement.Models;
using HostelManagement.Reports;
using HostelManagement.Services;
using PdfSharp.Pdf.IO;
using Xunit;

namespace HostelManagement.Tests;

public sealed class AgreementServiceTests : TestDatabase
{
    private static readonly DateTime CheckInDate = DateTime.Today.AddDays(-10);
    private static readonly DateTime AgreementDate = new(2026, 10, 6);

    private Room TripleRoom(string number = "101", string gender = RoomGender.Male)
    {
        RoomService.UpdateRent(SharingTypeId(3), 90_000m);
        return RoomService.Save(new Room { HostelId = HostelId, RoomNumber = number, SharingTypeId = SharingTypeId(3), Gender = gender });
    }

    private int AddStudent(string name, string gender = RoomGender.Male, string address = "12 MG Road, Jaipur", IReadOnlyCollection<int>? services = null) =>
        StudentService.Save(
            new Student { StudentName = name, Gender = gender, Mobile = "9876543210", CollegeId = CollegeId, AdmissionDate = CheckInDate, Address = address },
            new Parent { ParentName = $"Parent of {name}", Mobile = "9812345678", Email = "parent@example.com" },
            extraServiceIds: services).StudentId;

    private static string Plain(string text) =>
        text.Replace(AgreementService.ValueStart.ToString(), "").Replace(AgreementService.ValueEnd.ToString(), "");

    // ---- Bed numbers ----

    [Fact]
    public void CheckIn_GivesTheLowestFreeBed_AndCheckOutFreesIt()
    {
        Room room = TripleRoom();
        int aman = AddStudent("Aman");
        int ravi = AddStudent("Ravi");
        int karan = AddStudent("Karan");

        Assert.Equal(1, AllocationService.CheckIn(aman, room.RoomId, CheckInDate).BedNumber);
        Assert.Equal(2, AllocationService.CheckIn(ravi, room.RoomId, CheckInDate).BedNumber);
        AllocationService.CheckOut(aman, DateTime.Today);
        Assert.Equal([1, 3], AllocationService.GetFreeBeds(room.RoomId));
        Assert.Equal(1, AllocationService.CheckIn(karan, room.RoomId, CheckInDate).BedNumber);
    }

    [Fact]
    public void CheckIn_ChosenBed_MustBeFreeAndInTheRoom()
    {
        Room room = TripleRoom();
        int aman = AddStudent("Aman");
        int ravi = AddStudent("Ravi");

        Assert.Equal(3, AllocationService.CheckIn(aman, room.RoomId, CheckInDate, bedNumber: 3).BedNumber);
        Assert.Contains("already taken", Assert.Throws<ValidationException>(
            () => AllocationService.CheckIn(ravi, room.RoomId, CheckInDate, bedNumber: 3)).Message);
        Assert.Contains("beds 1 to 3", Assert.Throws<ValidationException>(
            () => AllocationService.CheckIn(ravi, room.RoomId, CheckInDate, bedNumber: 4)).Message);
        Assert.Null(AllocationService.GetCurrentAllocation(ravi));
    }

    [Fact]
    public void Transfer_GetsABedInTheNewRoom()
    {
        Room first = TripleRoom("101");
        Room second = TripleRoom("102");
        int aman = AddStudent("Aman");
        AllocationService.CheckIn(aman, first.RoomId, CheckInDate, bedNumber: 2);

        RoomAllocation moved = AllocationService.Transfer(aman, second.RoomId, DateTime.Today, bedNumber: 3);

        Assert.Equal("102, bed 3", moved.RoomAndBed);
        Assert.Equal([1, 2, 3], AllocationService.GetFreeBeds(first.RoomId));
    }

    [Fact]
    public void Room_CannotBecomeSmallerThanABedInUse()
    {
        Room room = TripleRoom();
        AllocationService.CheckIn(AddStudent("Aman"), room.RoomId, CheckInDate, bedNumber: 3);
        room.SharingTypeId = SharingTypeId(2);

        Assert.Contains("Bed 3", Assert.Throws<ValidationException>(() => RoomService.Save(room)).Message);
    }

    // ---- Agreement ----

    [Fact]
    public void Prepare_FillsTheStudentParentAddressRoomBedAndFee()
    {
        Room room = TripleRoom();
        ServiceItem transport = ServiceItemService.GetServices(HostelId).Single(s => s.ServiceName == "Transport");
        transport.MonthlyRate = 1_500m;
        ServiceItemService.Save(transport);
        int aman = AddStudent("Aman Sharma", services: [transport.ServiceId]);
        AllocationService.CheckIn(aman, room.RoomId, CheckInDate, bedNumber: 2);

        AgreementDocument document = AgreementService.Prepare(aman, AgreementDate);
        string text = Plain(document.FilledText);

        Assert.Contains("on date 06 Oct 2026 between", text);
        Assert.Contains("Name Aman Sharma", text);
        Assert.Contains("S/o Parent of Aman Sharma", text);
        Assert.Contains("R/o 12 MG Road, Jaipur", text);
        Assert.Contains("Shri Balaji Boys Hostel", text);
        Assert.Contains("consisting of Room No. 101, one bed room with three separate beds belongs to three different students", text);
        Assert.Contains("allotted bed no. 2 in Room no. 101", text);
        Assert.Contains($"w.e.f {CheckInDate:dd MMM yyyy}", text);
        // 90,000 yearly rent + 12 x 1,500 transport.
        Assert.Contains("annual fee of Rs. 1,08,000/- (Rupees One Lakh Eight Thousand Only)", text);
        Assert.Contains($"{AgreementService.ValueStart}Aman Sharma{AgreementService.ValueEnd}", document.FilledText);
        Assert.DoesNotContain("{", text);
        Assert.DoesNotContain("COVID", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Prepare_GirlUsesDOAndGirlsHostel_MissingAddressLeavesADottedLine()
    {
        Room room = TripleRoom(gender: RoomGender.Female);
        int priya = AddStudent("Priya", RoomGender.Female, address: "");
        AllocationService.CheckIn(priya, room.RoomId, CheckInDate);

        string text = Plain(AgreementService.Prepare(priya, AgreementDate).FilledText);

        Assert.Contains("D/o Parent of Priya", text);
        Assert.Contains("Shri Balaji Girls Hostel", text);
        Assert.Contains("R/o ......", text);
        Assert.Contains("annual fee of Rs. 90,000/- (Rupees Ninety Thousand Only)", text);
    }

    [Fact]
    public void Prepare_StudentWithoutRoom_IsRejected()
    {
        int aman = AddStudent("Aman");

        Assert.Contains("not been checked in", Assert.Throws<ValidationException>(() => AgreementService.Prepare(aman, AgreementDate)).Message);
    }

    [Fact]
    public void Template_CanBeEditedAndRestored()
    {
        Assert.Equal(AgreementService.DefaultTemplate, AgreementService.GetTemplate());
        Assert.Contains("40. All disputes are subject to Dehradun court Jurisdiction.", AgreementService.DefaultTemplate);

        AgreementService.SaveTemplate("# AGREEMENT\nRoom {RoomNumber}, bed {BedNumber}, fee Rs. {AnnualRent}");
        Room room = TripleRoom();
        int aman = AddStudent("Aman");
        AllocationService.CheckIn(aman, room.RoomId, CheckInDate);
        Assert.Equal("# AGREEMENT" + Environment.NewLine + "Room 101, bed 1, fee Rs. 90,000/-",
            Plain(AgreementService.Prepare(aman, AgreementDate).FilledText));

        var ex = Assert.Throws<ValidationException>(() => AgreementService.SaveTemplate("Bed {BedNo}"));
        Assert.Contains("{BedNo}", ex.Message);
        Assert.Contains("{BedNumber}", ex.Message);
        Assert.Throws<ValidationException>(() => AgreementService.SaveTemplate("   "));
    }

    [Fact]
    public void Pdf_IsWrittenOnSeveralPagesToTheAgreementsFolder()
    {
        Room room = TripleRoom();
        int aman = AddStudent("Aman Sharma");
        AllocationService.CheckIn(aman, room.RoomId, CheckInDate);
        AgreementDocument document = AgreementService.Prepare(aman, AgreementDate);

        string path = AgreementPdfWriter.SaveToAgreementsFolder(document, AgreementDate);

        Assert.Equal(Path.Combine(DataFolder, "Agreements", "Agreement_Aman-Sharma_2026-10-06.pdf"), path);
        using PdfSharp.Pdf.PdfDocument pdf = PdfReader.Open(path, PdfDocumentOpenMode.Import);
        Assert.True(pdf.PageCount >= 3, $"Expected the full agreement on several pages, got {pdf.PageCount}.");
    }

    [Fact]
    public void Words_MarkFilledValuesBold()
    {
        string line = $"Name {AgreementService.ValueStart}Aman Sharma{AgreementService.ValueEnd} of Jaipur";

        Assert.Equal([("Name", false), ("Aman", true), ("Sharma", true), ("of", false), ("Jaipur", false)],
            AgreementPdfWriter.Words(line));
    }
}
