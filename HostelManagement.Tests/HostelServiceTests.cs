using HostelManagement.Models;
using HostelManagement.Services;
using Xunit;

namespace HostelManagement.Tests;

public sealed class HostelServiceTests : TestDatabase
{
    [Fact]
    public void NewDatabase_HasNoHostels()
    {
        Assert.Empty(HostelService.GetHostels());
    }

    [Fact]
    public void Save_AddsSeveralHostelsSortedByName()
    {
        HostelService.Save(new Hostel
        {
            HostelName = "  Green Valley Hostel ",
            Address = " Sector 14, Chandigarh ",
            Phone = "+91 98765 43210",
            Email = "office@greenvalley.in",
        });
        HostelService.Save(new Hostel { HostelName = "Boys Hostel" });

        List<Hostel> hostels = HostelService.GetHostels();
        Assert.Equal(["Boys Hostel", "Green Valley Hostel"], hostels.Select(h => h.HostelName));

        Hostel green = hostels[1];
        Assert.Equal("Sector 14, Chandigarh", green.Address);
        Assert.Equal("+91 98765 43210", green.Phone);
        Assert.Equal("office@greenvalley.in", green.Email);
        Assert.Null(green.UpdatedDate);
    }

    [Fact]
    public void Save_NewHostel_GetsItsOwnSingleDoubleTripleSharingTypes()
    {
        int first = AddHostel("First Hostel");
        int second = AddHostel("Second Hostel");

        List<SharingType> firstTypes = RoomService.GetSharingTypes(first);
        List<SharingType> secondTypes = RoomService.GetSharingTypes(second);

        Assert.Equal(["Single", "Double", "Triple"], firstTypes.Select(t => t.SharingName));
        Assert.Equal([1, 2, 3], firstTypes.Select(t => t.Capacity));
        Assert.All(firstTypes, t => Assert.Equal(0m, t.Rent));
        Assert.Empty(firstTypes.Select(t => t.SharingTypeId).Intersect(secondTypes.Select(t => t.SharingTypeId)));
    }

    [Fact]
    public void Save_EditUpdatesTheHostel()
    {
        int id = AddHostel("Old Name");

        HostelService.Save(new Hostel { HostelId = id, HostelName = "New Name", Phone = "9876543210" });

        Hostel saved = HostelService.GetHostel(id)!;
        Assert.Equal("New Name", saved.HostelName);
        Assert.Equal("9876543210", saved.Phone);
        Assert.NotNull(saved.UpdatedDate);
        Assert.Single(HostelService.GetHostels());
    }

    [Fact]
    public void Save_DuplicateNameIgnoringCase_IsRejected()
    {
        AddHostel("Boys Hostel");

        var ex = Assert.Throws<ValidationException>(() => AddHostel("boys hostel"));

        Assert.Contains("already exists", ex.Message);
    }

    [Theory]
    [InlineData("", "", "", "Please enter the hostel name.")]
    [InlineData("   ", "", "", "Please enter the hostel name.")]
    [InlineData("Hostel", "abc", "", "valid phone")]
    [InlineData("Hostel", "", "not-an-email", "valid email")]
    public void Save_InvalidInput_IsRejected(string name, string phone, string email, string expectedMessage)
    {
        var ex = Assert.Throws<ValidationException>(() =>
            HostelService.Save(new Hostel { HostelName = name, Phone = phone, Email = email }));

        Assert.Contains(expectedMessage, ex.Message);
        Assert.Equal(0, Count("Hostel"));
    }

    [Fact]
    public void Save_RaisesHostelsChanged()
    {
        int raised = 0;
        void Handler(object? sender, EventArgs e) => raised++;
        HostelService.HostelsChanged += Handler;
        try
        {
            AddHostel("Event Hostel");
        }
        finally
        {
            HostelService.HostelsChanged -= Handler;
        }

        Assert.Equal(1, raised);
    }

    [Fact]
    public void Delete_EmptyHostel_RemovesItAndItsSharingTypes()
    {
        int id = AddHostel("Empty Hostel");

        HostelService.Delete(id);

        Assert.Empty(HostelService.GetHostels());
        Assert.Equal(0, Count("SharingType"));
    }

    [Fact]
    public void Delete_HostelWithColleges_IsRejected()
    {
        AddCollege(HostelId, "Some College");

        var ex = Assert.Throws<ValidationException>(() => HostelService.Delete(HostelId));

        Assert.Contains("1 college(s)", ex.Message);
        Assert.Single(HostelService.GetHostels());
    }

    [Fact]
    public void GetHostels_CountsCollegesAndRooms()
    {
        AddCollege(HostelId, "College A");
        AddCollege(HostelId, "College B");
        RoomService.Save(new Room { HostelId = HostelId, RoomNumber = "101", SharingTypeId = SharingTypeId(1) });

        Hostel hostel = HostelService.GetHostel(HostelId)!;

        Assert.Equal(2, hostel.CollegeCount);
        Assert.Equal(1, hostel.RoomCount);
    }

    [Fact]
    public void HostelContext_SelectsRequestedOrFirstHostel()
    {
        int boys = AddHostel("Boys Hostel");
        int girls = AddHostel("Girls Hostel");

        HostelContext.Select(girls);
        Assert.Equal("Girls Hostel", HostelContext.CurrentHostel!.HostelName);

        HostelContext.Select(null);
        Assert.Equal(boys, HostelContext.CurrentHostelId);

        HostelService.Delete(boys);
        HostelContext.Refresh();
        Assert.Equal(girls, HostelContext.CurrentHostelId);
    }
}
