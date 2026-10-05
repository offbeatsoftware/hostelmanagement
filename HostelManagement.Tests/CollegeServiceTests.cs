using HostelManagement.Data;
using HostelManagement.Models;
using HostelManagement.Services;
using Xunit;

namespace HostelManagement.Tests;

public sealed class CollegeServiceTests : TestDatabase
{
    private static College Add(string name, string address = "", string phone = "") =>
        CollegeService.Save(new College { CollegeName = name, Address = address, Phone = phone });

    [Fact]
    public void Save_AddsCollege()
    {
        College college = Add("  Government College ", "Sector 11", "0172 2700000");

        Assert.True(college.CollegeId > 0);
        College saved = CollegeService.Search().Single();
        Assert.Equal("Government College", saved.CollegeName);
        Assert.Equal("Sector 11", saved.Address);
        Assert.Equal("0172 2700000", saved.Phone);
    }

    [Fact]
    public void Save_EditsExistingCollege()
    {
        College college = Add("Old Name");

        CollegeService.Save(new College { CollegeId = college.CollegeId, CollegeName = "New Name" });

        Assert.Equal("New Name", CollegeService.Search().Single().CollegeName);
    }

    [Fact]
    public void Save_EditKeepingSameName_IsAllowed()
    {
        College college = Add("Same Name");

        CollegeService.Save(new College { CollegeId = college.CollegeId, CollegeName = "Same Name", Phone = "9876543210" });

        Assert.Equal("9876543210", CollegeService.Search().Single().Phone);
    }

    [Fact]
    public void Save_DuplicateNameIgnoringCase_IsRejected()
    {
        Add("DAV College");

        var ex = Assert.Throws<ValidationException>(() => Add("dav college"));

        Assert.Contains("already exists", ex.Message);
        Assert.Single(CollegeService.Search());
    }

    [Fact]
    public void Save_RenameToExistingName_IsRejected()
    {
        Add("First College");
        College second = Add("Second College");

        Assert.Throws<ValidationException>(() =>
            CollegeService.Save(new College { CollegeId = second.CollegeId, CollegeName = "First College" }));
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
            CollegeService.Search().Select(c => c.CollegeName));
        Assert.Equal(["Arts College", "Science College"],
            CollegeService.Search("college").Select(c => c.CollegeName));
    }

    [Fact]
    public void Search_TreatsWildcardCharactersLiterally()
    {
        Add("100% Placement College");
        Add("Other College");

        Assert.Single(CollegeService.Search("100%"));
        Assert.Empty(CollegeService.Search("_"));
    }

    [Fact]
    public void Delete_UnusedCollege_Succeeds()
    {
        College college = Add("Unused College");

        CollegeService.Delete(college.CollegeId);

        Assert.Empty(CollegeService.Search());
    }

    [Fact]
    public void Delete_CollegeWithStudents_IsRejected()
    {
        College college = Add("Busy College");
        Db.Execute("INSERT INTO [Student] ([StudentName], [CollegeId], [Status]) VALUES (?, ?, ?)",
            Db.Param("@StudentName", "Student"), Db.Param("@CollegeId", college.CollegeId), Db.Param("@Status", "Active"));

        var ex = Assert.Throws<ValidationException>(() => CollegeService.Delete(college.CollegeId));

        Assert.Contains("1 student", ex.Message);
        Assert.Single(CollegeService.Search());
    }
}
