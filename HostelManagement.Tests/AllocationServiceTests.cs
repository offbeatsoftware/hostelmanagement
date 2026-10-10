using HostelManagement.Models;
using HostelManagement.Services;
using Xunit;

namespace HostelManagement.Tests;

public sealed class AllocationServiceTests : TestDatabase
{
    private static readonly DateTime Admission = DateTime.Today.AddDays(-60);

    private int NewStudent(string name, string gender = RoomGender.Male, int? collegeId = null) =>
        StudentService.Save(
            new Student
            {
                StudentName = name,
                Gender = gender,
                Mobile = "9876543210",
                CollegeId = collegeId ?? CollegeId,
                AdmissionDate = Admission,
                FatherName = "Father",
                FatherMobile = "9812345678",
            }).StudentId;

    private Room NewRoom(string number, int capacity = 2, string gender = RoomGender.Male, bool active = true,
        int? hostelId = null) =>
        RoomService.Save(new Room
        {
            HostelId = hostelId ?? HostelId,
            RoomNumber = number,
            SharingTypeId = SharingTypeId(capacity, hostelId),
            Gender = gender,
            IsActive = active,
        });

    private Room Reload(Room room) => RoomService.GetRooms(room.HostelId).Single(r => r.RoomId == room.RoomId);

    private static DateTime DaysAgo(int days) => DateTime.Today.AddDays(-days);

    // ---- Check-in ----

    [Fact]
    public void CheckIn_PutsStudentInRoomAndCountsOccupancy()
    {
        int aman = NewStudent("Aman");
        Room room = NewRoom("101");

        RoomAllocation allocation = AllocationService.CheckIn(aman, room.RoomId, DaysAgo(10), " Bed near window ");

        Assert.Equal(AllocationStatus.Current, allocation.Status);
        Assert.Equal(DaysAgo(10), allocation.CheckInDate);
        Assert.Null(allocation.CheckOutDate);
        Assert.Equal("Bed near window", allocation.Remarks);
        Assert.Equal(10, allocation.Days);
        Assert.Equal(1, Reload(room).Occupied);
        Assert.Equal("101", StudentService.GetStudents(HostelId).Single().RoomNumber);
    }

    [Fact]
    public void CheckIn_StudentAlreadyInRoom_IsRejected()
    {
        int aman = NewStudent("Aman");
        Room first = NewRoom("101");
        Room second = NewRoom("102");
        AllocationService.CheckIn(aman, first.RoomId, DaysAgo(5));

        var ex = Assert.Throws<ValidationException>(() => AllocationService.CheckIn(aman, second.RoomId, DateTime.Today));

        Assert.Contains("already in room 101", ex.Message);
    }

    [Fact]
    public void CheckIn_RoomOfOtherGender_IsRejected()
    {
        int priya = NewStudent("Priya", RoomGender.Female);
        Room boysRoom = NewRoom("101", gender: RoomGender.Male);

        var ex = Assert.Throws<ValidationException>(() => AllocationService.CheckIn(priya, boysRoom.RoomId, DateTime.Today));

        Assert.Contains("is for boys", ex.Message);
        Assert.Equal(0, Reload(boysRoom).Occupied);
    }

    [Fact]
    public void CheckIn_FullRoom_IsRejected()
    {
        Room single = NewRoom("101", capacity: 1);
        AllocationService.CheckIn(NewStudent("First"), single.RoomId, DaysAgo(3));

        var ex = Assert.Throws<ValidationException>(() =>
            AllocationService.CheckIn(NewStudent("Second"), single.RoomId, DateTime.Today));

        Assert.Contains("full", ex.Message);
    }

    [Fact]
    public void CheckIn_InactiveRoom_IsRejected()
    {
        Room closed = NewRoom("101", active: false);

        var ex = Assert.Throws<ValidationException>(() =>
            AllocationService.CheckIn(NewStudent("Aman"), closed.RoomId, DateTime.Today));

        Assert.Contains("inactive", ex.Message);
    }

    [Fact]
    public void CheckIn_RoomOfAnotherHostel_IsRejected()
    {
        int otherHostel = AddHostel("Other Hostel");
        Room otherRoom = NewRoom("101", hostelId: otherHostel);

        var ex = Assert.Throws<ValidationException>(() =>
            AllocationService.CheckIn(NewStudent("Aman"), otherRoom.RoomId, DateTime.Today));

        Assert.Contains("another hostel", ex.Message);
    }

    [Fact]
    public void CheckIn_StudentWithoutGender_IsRejected()
    {
        int student = NewStudent("No Gender", gender: "");

        var ex = Assert.Throws<ValidationException>(() =>
            AllocationService.CheckIn(student, NewRoom("101").RoomId, DateTime.Today));

        Assert.Contains("gender", ex.Message);
    }

    [Fact]
    public void CheckIn_StudentWhoLeft_IsRejected()
    {
        int aman = NewStudent("Aman");
        Student student = StudentService.GetStudent(aman)!;
        student.Status = StudentStatus.Left;
        StudentService.Save(student);

        var ex = Assert.Throws<ValidationException>(() => AllocationService.CheckIn(aman, NewRoom("101").RoomId, DateTime.Today));

        Assert.Contains("Set the status to Active", ex.Message);
    }

    [Fact]
    public void CheckIn_DatesAreChecked()
    {
        int aman = NewStudent("Aman");
        Room room = NewRoom("101");

        Assert.Contains("admission date", Assert.Throws<ValidationException>(() =>
            AllocationService.CheckIn(aman, room.RoomId, Admission.AddDays(-1))).Message);
        Assert.Contains("future", Assert.Throws<ValidationException>(() =>
            AllocationService.CheckIn(aman, room.RoomId, DateTime.Today.AddDays(1))).Message);
    }

    // ---- Transfer ----

    [Fact]
    public void Transfer_LeavesOldRoomAndEntersNewRoomOnTheSameDay()
    {
        int aman = NewStudent("Aman");
        Room oldRoom = NewRoom("101");
        Room newRoom = NewRoom("102");
        AllocationService.CheckIn(aman, oldRoom.RoomId, DaysAgo(20), "First room");

        RoomAllocation moved = AllocationService.Transfer(aman, newRoom.RoomId, DaysAgo(5), "Asked for a quieter room");

        List<RoomAllocation> history = AllocationService.GetHistory(aman);
        Assert.Equal(2, history.Count);
        Assert.Equal(AllocationStatus.Transferred, history[0].Status);
        Assert.Equal(DaysAgo(5), history[0].CheckOutDate);
        Assert.Equal("First room | Asked for a quieter room", history[0].Remarks);
        Assert.Equal(AllocationStatus.Current, moved.Status);
        Assert.Equal("102", moved.RoomNumber);
        Assert.Equal(DaysAgo(5), moved.CheckInDate);
        Assert.Equal(0, Reload(oldRoom).Occupied);
        Assert.Equal(1, Reload(newRoom).Occupied);
    }

    [Fact]
    public void Transfer_Rules()
    {
        int aman = NewStudent("Aman");
        Room room = NewRoom("101");
        Room girlsRoom = NewRoom("201", gender: RoomGender.Female);
        Room other = NewRoom("102");

        Assert.Contains("not in a room", Assert.Throws<ValidationException>(() =>
            AllocationService.Transfer(aman, other.RoomId, DateTime.Today)).Message);

        AllocationService.CheckIn(aman, room.RoomId, DaysAgo(3));

        Assert.Contains("already in room", Assert.Throws<ValidationException>(() =>
            AllocationService.Transfer(aman, room.RoomId, DateTime.Today)).Message);
        Assert.Contains("before the check-in date", Assert.Throws<ValidationException>(() =>
            AllocationService.Transfer(aman, other.RoomId, DaysAgo(4))).Message);
        Assert.Contains("is for girls", Assert.Throws<ValidationException>(() =>
            AllocationService.Transfer(aman, girlsRoom.RoomId, DateTime.Today)).Message);

        // Nothing changed after the rejected transfers.
        Assert.Single(AllocationService.GetHistory(aman));
        Assert.Equal(1, Reload(room).Occupied);
    }

    // ---- Check-out ----

    [Fact]
    public void CheckOut_FreesTheBedAndSetsStudentToLeft()
    {
        int aman = NewStudent("Aman");
        Room room = NewRoom("101", capacity: 1);
        AllocationService.CheckIn(aman, room.RoomId, DaysAgo(30));

        AllocationService.CheckOut(aman, DaysAgo(1), "Course completed");

        RoomAllocation closed = AllocationService.GetHistory(aman).Single();
        Assert.Equal(AllocationStatus.CheckedOut, closed.Status);
        Assert.Equal(DaysAgo(1), closed.CheckOutDate);
        Assert.Equal(29, closed.Days);
        Assert.Equal(StudentStatus.Left, StudentService.GetStudent(aman)!.Status);
        Assert.Null(AllocationService.GetCurrentAllocation(aman));

        // The checked-out student no longer counts: the single room is free again.
        Assert.Equal(0, Reload(room).Occupied);
        AllocationService.CheckIn(NewStudent("Next"), room.RoomId, DateTime.Today);
    }

    [Fact]
    public void CheckOut_Rules()
    {
        int aman = NewStudent("Aman");

        Assert.Contains("not in a room", Assert.Throws<ValidationException>(() =>
            AllocationService.CheckOut(aman, DateTime.Today)).Message);

        AllocationService.CheckIn(aman, NewRoom("101").RoomId, DaysAgo(2));

        Assert.Contains("before the check-in date", Assert.Throws<ValidationException>(() =>
            AllocationService.CheckOut(aman, DaysAgo(3))).Message);
        Assert.Contains("future", Assert.Throws<ValidationException>(() =>
            AllocationService.CheckOut(aman, DateTime.Today.AddDays(1))).Message);
    }

    // ---- Lists and student rules ----

    [Fact]
    public void Lists_CurrentAllocationsHistoryStudentsWithoutRoomAndRoomsForGender()
    {
        int aman = NewStudent("Aman");
        int ravi = NewStudent("Ravi");
        int priya = NewStudent("Priya", RoomGender.Female);
        NewStudent("Waiting");
        Room boysDouble = NewRoom("101");
        Room boysSingle = NewRoom("102", capacity: 1);
        Room girls = NewRoom("201", gender: RoomGender.Female);
        NewRoom("103", active: false);

        AllocationService.CheckIn(aman, boysSingle.RoomId, DaysAgo(10));
        AllocationService.CheckIn(ravi, boysDouble.RoomId, DaysAgo(10));
        AllocationService.CheckIn(priya, girls.RoomId, DaysAgo(10));
        AllocationService.CheckOut(priya, DaysAgo(1));

        Assert.Equal(["101", "102"], AllocationService.GetAllocations(HostelId).Select(a => a.RoomNumber));
        Assert.Equal(3, AllocationService.GetAllocations(HostelId, includeHistory: true).Count);
        Assert.Equal(["Waiting"], AllocationService.GetStudentsWithoutRoom(HostelId).Select(s => s.StudentName));
        Assert.Equal(["101"], AllocationService.GetRoomsFor(HostelId, RoomGender.Male).Select(r => r.RoomNumber));
        Assert.Equal(["201"], AllocationService.GetRoomsFor(HostelId, RoomGender.Female).Select(r => r.RoomNumber));
        Assert.Empty(AllocationService.GetRoomsFor(HostelId, RoomGender.Male, exceptRoomId: boysDouble.RoomId));
    }

    [Fact]
    public void Transfer_And_CheckOut_KeepTheFee()
    {
        int aman = NewStudent("Aman");
        AllocationService.CheckIn(aman, NewRoom("101").RoomId, Admission, fee: new YearFee(50_000m, 12_000m));
        AllocationService.Transfer(aman, NewRoom("102").RoomId, DateTime.Today);
        AllocationService.CheckOut(aman, DateTime.Today);

        Invoice fee = Assert.Single(InvoiceService.GetInvoicesForStudent(aman));
        Assert.Equal(62_000m, fee.TotalAmount);
        Assert.Equal(AcademicYear.Of(Admission), fee.AcademicYear);
    }

    [Fact]
    public void Student_InRoom_CannotBeSetToLeftOrChangeGenderOnStudentScreen()
    {
        int aman = NewStudent("Aman");
        AllocationService.CheckIn(aman, NewRoom("101").RoomId, DateTime.Today);

        Student leaving = StudentService.GetStudent(aman)!;
        leaving.Status = StudentStatus.Left;
        Assert.Contains("Use Check-out", Assert.Throws<ValidationException>(() => StudentService.Save(leaving)).Message);

        Student changing = StudentService.GetStudent(aman)!;
        changing.Gender = RoomGender.Female;
        Assert.Contains("gender cannot be changed", Assert.Throws<ValidationException>(() => StudentService.Save(changing)).Message);
    }
}
