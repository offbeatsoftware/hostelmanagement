using HostelManagement.Models;
using HostelManagement.Services;
using Xunit;

namespace HostelManagement.Tests;

public sealed class ParentServiceTests : TestDatabase
{
    private int AddStudentWithParent(string studentName = "Aman", int? collegeId = null) =>
        StudentService.Save(
            new Student
            {
                StudentName = studentName,
                Mobile = "9876543210",
                CollegeId = collegeId ?? CollegeId,
                AdmissionDate = new DateTime(2026, 7, 1),
            },
            new Parent { ParentName = "Father", Mobile = "9812345678", Email = "father@example.com" }).StudentId;

    private static Parent AddGuardian(int studentId, string name, bool primary = false) =>
        ParentService.Save(new Parent
        {
            StudentId = studentId,
            ParentName = name,
            Relationship = "Guardian",
            Mobile = "9898989898",
            Email = "guardian@example.com",
            IsPrimaryContact = primary,
        });

    [Fact]
    public void Save_AddsExtraGuardianAsSecondaryContact()
    {
        int studentId = AddStudentWithParent();

        AddGuardian(studentId, "Uncle");

        List<Parent> parents = ParentService.GetParentsOfStudent(studentId);
        Assert.Equal(2, parents.Count);
        Assert.Equal("Father", parents.Single(p => p.IsPrimaryContact).ParentName);
    }

    [Fact]
    public void Save_MarkingAnotherParentPrimary_ReplacesThePrimaryContact()
    {
        int studentId = AddStudentWithParent();

        AddGuardian(studentId, "Mother", primary: true);

        List<Parent> parents = ParentService.GetParentsOfStudent(studentId);
        Assert.Equal("Mother", parents.Single(p => p.IsPrimaryContact).ParentName);
        Assert.Equal("Mother", StudentService.GetStudents(HostelId).Single().ParentName);
    }

    [Fact]
    public void Save_UncheckingTheOnlyPrimary_IsRejected()
    {
        int studentId = AddStudentWithParent();
        Parent father = StudentService.GetPrimaryParent(studentId)!;

        father.IsPrimaryContact = false;
        var ex = Assert.Throws<ValidationException>(() => ParentService.Save(father));

        Assert.Contains("primary contact", ex.Message);
    }

    [Fact]
    public void Save_RequiresNameMobileEmailAndStudent()
    {
        int studentId = AddStudentWithParent();

        Assert.Throws<ValidationException>(() => ParentService.Save(new Parent { StudentId = studentId, ParentName = "X", Mobile = "9898989898" }));
        Assert.Throws<ValidationException>(() => ParentService.Save(new Parent { StudentId = studentId, ParentName = "X", Email = "x@y.com" }));
        Assert.Contains("student", Assert.Throws<ValidationException>(() => ParentService.Save(new Parent
        {
            StudentId = 9999, ParentName = "X", Mobile = "9898989898", Email = "x@y.com",
        })).Message);
    }

    [Fact]
    public void Delete_OnlyParent_IsRejected()
    {
        int studentId = AddStudentWithParent();
        Parent father = StudentService.GetPrimaryParent(studentId)!;

        var ex = Assert.Throws<ValidationException>(() => ParentService.Delete(father.ParentId));

        Assert.Contains("only parent", ex.Message);
    }

    [Fact]
    public void Delete_PrimaryParent_MakesAnotherParentPrimary()
    {
        int studentId = AddStudentWithParent();
        Parent father = StudentService.GetPrimaryParent(studentId)!;
        AddGuardian(studentId, "Mother");

        ParentService.Delete(father.ParentId);

        Parent remaining = ParentService.GetParentsOfStudent(studentId).Single();
        Assert.Equal("Mother", remaining.ParentName);
        Assert.True(remaining.IsPrimaryContact);
    }

    [Fact]
    public void GetParents_ShowsOnlyTheHostelsParents()
    {
        AddStudentWithParent("Ours");
        AddStudentWithParent("Theirs", AddCollege(AddHostel("Other Hostel"), "Other College"));

        Assert.Equal(["Ours"], ParentService.GetParents(HostelId).Select(p => p.StudentName));
    }
}
