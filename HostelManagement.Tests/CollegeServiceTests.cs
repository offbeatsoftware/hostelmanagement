using HostelManagement.Models;
using HostelManagement.Services;
using Xunit;

namespace HostelManagement.Tests;

public sealed class CollegeServiceTests : TestDatabase
{
    private College Add(string name, string address = "", string phone = "", int? hostelId = null) =>
        CollegeService.Save(new College
        {
            HostelId = hostelId ?? HostelId,
            CollegeName = name,
            Address = address,
            Phone = phone,
        });

    [Fact]
    public void Save_AddsCollegeToTheHostel()
    {
        College college = Add("  Government College ", "Sector 11", "0172 2700000");

        Assert.True(college.CollegeId > 0);
        College saved = CollegeService.Search(HostelId).Single();
        Assert.Equal(HostelId, saved.HostelId);
        Assert.Equal("Government College", saved.CollegeName);
        Assert.Equal("Sector 11", saved.Address);
        Assert.Equal("0172 2700000", saved.Phone);
    }

    [Fact]
    public void Search_ShowsOnlyTheHostelsColleges()
    {
        int otherHostel = AddHostel("Other Hostel");
        Add("Our College");
        Add("Their College", hostelId: otherHostel);

        Assert.Equal(["Our College"], CollegeService.Search(HostelId).Select(c => c.CollegeName));
        Assert.Equal(["Their College"], CollegeService.Search(otherHostel).Select(c => c.CollegeName));
    }

    [Fact]
    public void Save_SameCollegeNameInDifferentHostels_IsAllowed()
    {
        int otherHostel = AddHostel("Other Hostel");

        Add("DAV College");
        Add("DAV College", hostelId: otherHostel);

        Assert.Single(CollegeService.Search(HostelId));
        Assert.Single(CollegeService.Search(otherHostel));
    }

    [Fact]
    public void Save_DuplicateNameInSameHostelIgnoringCase_IsRejected()
    {
        Add("DAV College");

        var ex = Assert.Throws<ValidationException>(() => Add("dav college"));

        Assert.Contains("already exists in this hostel", ex.Message);
        Assert.Single(CollegeService.Search(HostelId));
    }

    [Fact]
    public void Save_EditsExistingCollegeAndKeepsItsHostel()
    {
        College college = Add("Old Name");
        int otherHostel = AddHostel("Other Hostel");

        CollegeService.Save(new College { CollegeId = college.CollegeId, HostelId = otherHostel, CollegeName = "New Name" });

        Assert.Equal("New Name", CollegeService.Search(HostelId).Single().CollegeName);
        Assert.Empty(CollegeService.Search(otherHostel));
    }

    [Fact]
    public void Save_EditKeepingSameName_IsAllowed()
    {
        College college = Add("Same Name");

        CollegeService.Save(new College { CollegeId = college.CollegeId, CollegeName = "Same Name", Phone = "9876543210" });

        Assert.Equal("9876543210", CollegeService.Search(HostelId).Single().Phone);
    }

    [Fact]
    public void Save_WithoutValidHostel_IsRejected()
    {
        var ex = Assert.Throws<ValidationException>(() =>
            CollegeService.Save(new College { HostelId = 9999, CollegeName = "Orphan College" }));

        Assert.Contains("hostel", ex.Message);
    }

    [Theory]
    [InlineData("", "", "Please enter the college name.")]
    [InlineData("College", "phone", "valid phone")]
    public void Save_InvalidInput_IsRejected(string name, string phone, string expectedMessage)
    {
        var ex = Assert.Throws<ValidationException>(() => Add(name, phone: phone));

        Assert.Contains(expectedMessage, ex.Message);
    }

    [Fact]
    public void Search_FiltersByPartOfNameAndSortsByName()
    {
        Add("Zenith Institute");
        Add("Arts College");
        Add("Science College");

        Assert.Equal(["Arts College", "Science College", "Zenith Institute"],
            CollegeService.Search(HostelId).Select(c => c.CollegeName));
        Assert.Equal(["Arts College", "Science College"],
            CollegeService.Search(HostelId, "college").Select(c => c.CollegeName));
    }

    [Fact]
    public void Search_TreatsWildcardCharactersLiterally()
    {
        Add("100% Placement College");
        Add("Other College");

        Assert.Single(CollegeService.Search(HostelId, "100%"));
        Assert.Empty(CollegeService.Search(HostelId, "_"));
    }

    [Fact]
    public void Delete_UnusedCollege_Succeeds()
    {
        College college = Add("Unused College");

        CollegeService.Delete(college.CollegeId);

        Assert.Empty(CollegeService.Search(HostelId));
    }

    [Fact]
    public void Delete_CollegeWithStudents_IsRejected()
    {
        College college = Add("Busy College");
        AddStudent("Student", college.CollegeId);

        var ex = Assert.Throws<ValidationException>(() => CollegeService.Delete(college.CollegeId));

        Assert.Contains("1 student", ex.Message);
        Assert.Single(CollegeService.Search(HostelId));
    }
}
