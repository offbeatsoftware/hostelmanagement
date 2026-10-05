using HostelManagement.Models;
using HostelManagement.Services;
using Xunit;

namespace HostelManagement.Tests;

public sealed class HostelServiceTests : TestDatabase
{
    [Fact]
    public void GetDetails_BeforeFirstSave_ReturnsNull()
    {
        Assert.Null(HostelService.GetDetails());
        Assert.Equal(string.Empty, HostelService.GetHostelName());
    }

    [Fact]
    public void Save_FirstTime_AddsTrimmedDetails()
    {
        HostelService.Save(new HostelDetails
        {
            HostelName = "  Green Valley Hostel ",
            Address = " Sector 14, Chandigarh ",
            Phone = "+91 98765 43210",
            Email = "office@greenvalley.in",
        });

        HostelDetails saved = HostelService.GetDetails()!;
        Assert.Equal("Green Valley Hostel", saved.HostelName);
        Assert.Equal("Sector 14, Chandigarh", saved.Address);
        Assert.Equal("+91 98765 43210", saved.Phone);
        Assert.Equal("office@greenvalley.in", saved.Email);
        Assert.Null(saved.UpdatedDate);
    }

    [Fact]
    public void Save_SecondTime_UpdatesTheSameRow()
    {
        HostelService.Save(new HostelDetails { HostelName = "First Name", Phone = "9876543210" });
        DateTime created = HostelService.GetDetails()!.CreatedDate;

        HostelService.Save(new HostelDetails { HostelName = "Second Name" });

        HostelDetails saved = HostelService.GetDetails()!;
        Assert.Equal(1, Count("Hostel"));
        Assert.Equal("Second Name", saved.HostelName);
        Assert.Equal(string.Empty, saved.Phone);
        Assert.NotNull(saved.UpdatedDate);
        Assert.True(Math.Abs((saved.CreatedDate - created).TotalSeconds) < 1);
    }

    [Fact]
    public void Save_OnlyHostelNameIsRequired()
    {
        HostelService.Save(new HostelDetails { HostelName = "Name Only" });

        Assert.Equal("Name Only", HostelService.GetHostelName());
    }

    [Theory]
    [InlineData("", "", "", "Please enter the hostel name.")]
    [InlineData("   ", "", "", "Please enter the hostel name.")]
    [InlineData("Hostel", "abc", "", "valid phone")]
    [InlineData("Hostel", "", "not-an-email", "valid email")]
    public void Save_InvalidInput_IsRejected(string name, string phone, string email, string expectedMessage)
    {
        var ex = Assert.Throws<ValidationException>(() =>
            HostelService.Save(new HostelDetails { HostelName = name, Phone = phone, Email = email }));

        Assert.Contains(expectedMessage, ex.Message);
        Assert.Equal(0, Count("Hostel"));
    }

    [Fact]
    public void Save_RaisesDetailsSaved()
    {
        int raised = 0;
        void Handler(object? sender, EventArgs e) => raised++;
        HostelService.DetailsSaved += Handler;
        try
        {
            HostelService.Save(new HostelDetails { HostelName = "Event Hostel" });
        }
        finally
        {
            HostelService.DetailsSaved -= Handler;
        }

        Assert.Equal(1, raised);
    }
}
