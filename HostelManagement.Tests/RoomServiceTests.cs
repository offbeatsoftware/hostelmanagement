using HostelManagement.Models;
using HostelManagement.Services;
using Xunit;

namespace HostelManagement.Tests;

public sealed class RoomServiceTests : TestDatabase
{
    private Room AddRoom(string number, int capacity, bool active = true, string floor = "", int? hostelId = null) =>
        RoomService.Save(new Room
        {
            HostelId = hostelId ?? HostelId,
            RoomNumber = number,
            Floor = floor,
            SharingTypeId = SharingTypeId(capacity, hostelId),
            IsActive = active,
        });

    private Room Find(string number) => RoomService.GetRooms(HostelId).Single(r => r.RoomNumber == number);

    [Fact]
    public void NewHostel_HasSingleDoubleTripleWithMatchingCapacity()
    {
        List<SharingType> types = RoomService.GetSharingTypes(HostelId);

        Assert.Equal(["Single", "Double", "Triple"], types.Select(t => t.SharingName));
        Assert.Equal([1, 2, 3], types.Select(t => t.Capacity));
        Assert.All(types, t => Assert.Equal(0m, t.Rent));
    }

    [Fact]
    public void Initialize_RunAgain_DoesNotDuplicateSharingTypes()
    {
        _ = HostelId;
        Data.DatabaseInitializer.Initialize();

        Assert.Equal(3, Count("SharingType"));
    }

    [Fact]
    public void Rent_IsSeparateForEachHostel()
    {
        int otherHostel = AddHostel("Other Hostel");
        AddRoom("101", capacity: 2);
        AddRoom("101", capacity: 2, hostelId: otherHostel);

        RoomService.UpdateRent(SharingTypeId(2), 4500m);
        RoomService.UpdateRent(SharingTypeId(2, otherHostel), 6000m);

        Assert.Equal(4500m, Find("101").Rent);
        Assert.Equal(6000m, RoomService.GetRooms(otherHostel).Single().Rent);
    }

    [Fact]
    public void GetRooms_ShowsOnlyTheHostelsRooms()
    {
        int otherHostel = AddHostel("Other Hostel");
        AddRoom("101", capacity: 1);
        AddRoom("201", capacity: 1, hostelId: otherHostel);

        Assert.Equal(["101"], RoomService.GetRooms(HostelId).Select(r => r.RoomNumber));
        Assert.Equal(["201"], RoomService.GetRooms(otherHostel).Select(r => r.RoomNumber));
    }

    [Fact]
    public void Save_SharingTypeOfAnotherHostel_IsRejected()
    {
        int otherHostel = AddHostel("Other Hostel");

        var ex = Assert.Throws<ValidationException>(() => RoomService.Save(new Room
        {
            HostelId = HostelId,
            RoomNumber = "101",
            SharingTypeId = SharingTypeId(1, otherHostel),
        }));

        Assert.Contains("sharing type", ex.Message);
    }

    [Fact]
    public void Save_WithoutValidHostel_IsRejected()
    {
        var ex = Assert.Throws<ValidationException>(() => RoomService.Save(new Room
        {
            HostelId = 9999,
            RoomNumber = "101",
            SharingTypeId = SharingTypeId(1),
        }));

        Assert.Contains("hostel", ex.Message);
    }

    [Fact]
    public void UpdateRent_AppliesToEveryRoomOfThatSharingType()
    {
        AddRoom("101", capacity: 2);
        AddRoom("102", capacity: 2);
        AddRoom("103", capacity: 3);

        RoomService.UpdateRent(SharingTypeId(2), 4500m);
        RoomService.UpdateRent(SharingTypeId(3), 3800.50m);

        Assert.Equal(4500m, Find("101").Rent);
        Assert.Equal(4500m, Find("102").Rent);
        Assert.Equal(3800.50m, Find("103").Rent);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    [InlineData(1_000_001)]
    [InlineData(100.555)]
    public void UpdateRent_InvalidAmount_IsRejected(decimal rent)
    {
        Assert.Throws<ValidationException>(() => RoomService.UpdateRent(SharingTypeId(1), rent));
    }

    [Fact]
    public void Save_AddsRoomWithCapacityFromSharingType()
    {
        Room room = AddRoom(" 101 ", capacity: 3, floor: " Ground ");

        Assert.True(room.RoomId > 0);
        Assert.Equal("101", room.RoomNumber);
        Assert.Equal("Ground", room.Floor);
        Assert.Equal("Triple", room.SharingName);
        Assert.Equal(3, room.Capacity);
        Assert.Equal(0, room.Occupied);
        Assert.Equal(3, room.Available);
        Assert.Equal("Active", room.Status);
    }

    [Fact]
    public void Save_DuplicateRoomNumberIgnoringCase_IsRejected()
    {
        AddRoom("A-1", capacity: 1);

        var ex = Assert.Throws<ValidationException>(() => AddRoom("a-1", capacity: 2));

        Assert.Contains("already exists in this hostel", ex.Message);
        Assert.Single(RoomService.GetRooms(HostelId));
    }

    [Fact]
    public void Save_RequiresRoomNumberAndSharingType()
    {
        Assert.Contains("room number",
            Assert.Throws<ValidationException>(() => AddRoom("  ", capacity: 1)).Message);
        Assert.Contains("sharing type",
            Assert.Throws<ValidationException>(() =>
                RoomService.Save(new Room { HostelId = HostelId, RoomNumber = "101", SharingTypeId = 0 })).Message);
    }

    [Fact]
    public void GetRooms_CountsOnlyCurrentAllocations()
    {
        Room room = AddRoom("201", capacity: 3);
        AddAllocation(room.RoomId);
        AddAllocation(room.RoomId);
        AddAllocation(room.RoomId, AllocationStatus.CheckedOut);
        AddAllocation(room.RoomId, AllocationStatus.Transferred);

        Room loaded = Find("201");

        Assert.Equal(2, loaded.Occupied);
        Assert.Equal(1, loaded.Available);
    }

    [Fact]
    public void GetRooms_SortsRoomNumbersNaturally()
    {
        foreach (string number in new[] { "10", "2", "A-10", "1", "A-9" })
        {
            AddRoom(number, capacity: 1);
        }

        Assert.Equal(["1", "2", "10", "A-9", "A-10"], RoomService.GetRooms(HostelId).Select(r => r.RoomNumber));
    }

    [Fact]
    public void InactiveRoom_HasNoFreeBeds()
    {
        Room room = AddRoom("301", capacity: 2, active: false);

        Assert.Equal(0, room.Available);
        Assert.Equal("Inactive", room.Status);
    }

    [Fact]
    public void Edit_ChangingToSmallerSharingThanOccupancy_IsRejected()
    {
        Room room = AddRoom("401", capacity: 3);
        AddAllocation(room.RoomId);
        AddAllocation(room.RoomId);

        room.SharingTypeId = SharingTypeId(1);
        var ex = Assert.Throws<ValidationException>(() => RoomService.Save(room));

        Assert.Contains("has 2 students", ex.Message);
        Assert.Equal(3, Find("401").Capacity);
    }

    [Fact]
    public void Edit_ChangingToSharingThatStillFits_IsAllowed()
    {
        Room room = AddRoom("402", capacity: 3);
        AddAllocation(room.RoomId);
        AddAllocation(room.RoomId);

        room.SharingTypeId = SharingTypeId(2);
        RoomService.Save(room);

        Room loaded = Find("402");
        Assert.Equal(2, loaded.Capacity);
        Assert.Equal(0, loaded.Available);
    }

    [Fact]
    public void Edit_DeactivatingOccupiedRoom_IsRejected()
    {
        Room room = AddRoom("501", capacity: 2);
        AddAllocation(room.RoomId);

        room.IsActive = false;
        var ex = Assert.Throws<ValidationException>(() => RoomService.Save(room));

        Assert.Contains("cannot be made inactive", ex.Message);
        Assert.True(Find("501").IsActive);
    }

    [Fact]
    public void Delete_RoomNeverUsed_Succeeds()
    {
        Room room = AddRoom("601", capacity: 1);

        RoomService.Delete(room.RoomId);

        Assert.Empty(RoomService.GetRooms(HostelId));
    }

    [Fact]
    public void Delete_RoomWithPastStudents_IsRejected()
    {
        Room room = AddRoom("602", capacity: 1);
        AddAllocation(room.RoomId, AllocationStatus.CheckedOut);

        var ex = Assert.Throws<ValidationException>(() => RoomService.Delete(room.RoomId));

        Assert.Contains("mark it inactive", ex.Message);
        Assert.Single(RoomService.GetRooms(HostelId));
    }

    [Fact]
    public void CheckCanAllocate_ActiveRoomWithFreeBed_IsAllowed()
    {
        Room room = AddRoom("701", capacity: 2);
        AddAllocation(room.RoomId);

        Assert.Equal(1, RoomService.CheckCanAllocate(room.RoomId).Available);
    }

    [Fact]
    public void CheckCanAllocate_FullRoom_IsRejected()
    {
        Room room = AddRoom("702", capacity: 1);
        AddAllocation(room.RoomId);

        var ex = Assert.Throws<ValidationException>(() => RoomService.CheckCanAllocate(room.RoomId));

        Assert.Contains("full", ex.Message);
    }

    [Fact]
    public void CheckCanAllocate_InactiveRoom_IsRejected()
    {
        Room room = AddRoom("703", capacity: 2, active: false);

        var ex = Assert.Throws<ValidationException>(() => RoomService.CheckCanAllocate(room.RoomId));

        Assert.Contains("inactive", ex.Message);
    }
}
