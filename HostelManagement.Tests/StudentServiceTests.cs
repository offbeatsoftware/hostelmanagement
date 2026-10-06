using System.Drawing.Imaging;
using HostelManagement.Models;
using HostelManagement.Services;
using HostelManagement.Utilities;
using Xunit;

namespace HostelManagement.Tests;

public sealed class StudentServiceTests : TestDatabase
{
    // Valid Aadhaar numbers (correct Verhoeff check digit) used only for testing.
    private const string Aadhaar1 = "234567890124";
    private const string Aadhaar2 = "987654321012";

    private Student NewStudent(string name = "Aman Sharma", string aadhaar = "", int? collegeId = null) => new()
    {
        StudentName = name,
        Mobile = "9876543210",
        CollegeId = collegeId ?? CollegeId,
        AdmissionDate = new DateTime(2026, 7, 1),
        AadhaarNumber = aadhaar,
        Status = StudentStatus.Active,
    };

    private static Parent NewParent(string name = "Rakesh Sharma") => new()
    {
        ParentName = name,
        Relationship = "Father",
        Mobile = "9812345678",
        Email = "rakesh@example.com",
    };

    private Student Add(Student? student = null, Parent? parent = null, StudentFileChanges? files = null) =>
        StudentService.Save(student ?? NewStudent(), parent ?? NewParent(), files);

    private string CreatePng(string name = "photo.png")
    {
        string file = Path.Combine(DataFolder, name);
        using var bitmap = new Bitmap(20, 30);
        bitmap.Save(file, ImageFormat.Png);
        return file;
    }

    private string CreatePdf(string name = "aadhaar.pdf")
    {
        string file = Path.Combine(DataFolder, name);
        File.WriteAllText(file, "%PDF-1.4\n% test file\n");
        return file;
    }

    [Fact]
    public void Save_AddsStudentWithPrimaryParent()
    {
        Student saved = Add(NewStudent(" Aman Sharma ", "2345 6789 0124"));

        Assert.True(saved.StudentId > 0);
        Assert.Equal("Aman Sharma", saved.StudentName);
        Assert.Equal(Aadhaar1, saved.AadhaarNumber);
        Assert.Equal("XXXX XXXX 0124", saved.AadhaarMasked);
        Assert.Equal("Test College", saved.CollegeName);

        Parent parent = StudentService.GetPrimaryParent(saved.StudentId)!;
        Assert.Equal("Rakesh Sharma", parent.ParentName);
        Assert.True(parent.IsPrimaryContact);
    }

    [Fact]
    public void GetStudents_ShowsOnlyTheHostelsStudentsWithParentDetails()
    {
        int otherCollege = AddCollege(AddHostel("Other Hostel"), "Other College");
        Add(NewStudent("Zoya"));
        Add(NewStudent("Aman"));
        Add(NewStudent("Elsewhere", collegeId: otherCollege));

        List<Student> students = StudentService.GetStudents(HostelId);

        Assert.Equal(["Aman", "Zoya"], students.Select(s => s.StudentName));
        Assert.All(students, s =>
        {
            Assert.Equal("Rakesh Sharma", s.ParentName);
            Assert.Equal("9812345678", s.ParentMobile);
        });
    }

    [Fact]
    public void Save_EditUpdatesStudentAndParent()
    {
        Student student = Add();
        Parent parent = StudentService.GetPrimaryParent(student.StudentId)!;

        student.StudentName = "Aman K Sharma";
        student.Status = StudentStatus.Left;
        parent.Email = "new@example.com";
        StudentService.Save(student, parent);

        Student saved = StudentService.GetStudent(student.StudentId)!;
        Assert.Equal("Aman K Sharma", saved.StudentName);
        Assert.Equal(StudentStatus.Left, saved.Status);
        Assert.Equal("new@example.com", StudentService.GetPrimaryParent(student.StudentId)!.Email);
        Assert.Equal(1, Count("Parent"));
    }

    [Theory]
    [InlineData("", "9876543210", "student's name")]
    [InlineData("Aman", "", "student's mobile")]
    [InlineData("Aman", "12ab", "valid mobile")]
    public void Save_StudentRequiredFields(string name, string mobile, string expected)
    {
        Student student = NewStudent(name);
        student.Mobile = mobile;

        var ex = Assert.Throws<ValidationException>(() => Add(student));

        Assert.Contains(expected, ex.Message);
        Assert.Equal(0, Count("Student"));
    }

    [Fact]
    public void Save_RequiresCollege()
    {
        Student student = NewStudent();
        student.CollegeId = 0;

        Assert.Contains("college", Assert.Throws<ValidationException>(() => Add(student)).Message);
    }

    [Theory]
    [InlineData("", "9812345678", "a@b.com", "parent or guardian's name")]
    [InlineData("Rakesh", "", "a@b.com", "parent or guardian's mobile")]
    [InlineData("Rakesh", "9812345678", "", "parent or guardian's email")]
    [InlineData("Rakesh", "9812345678", "not-an-email", "valid email")]
    public void Save_ParentNameMobileAndEmailAreRequired(string name, string mobile, string email, string expected)
    {
        var parent = new Parent { ParentName = name, Mobile = mobile, Email = email };

        var ex = Assert.Throws<ValidationException>(() => Add(parent: parent));

        Assert.Contains(expected, ex.Message);
        Assert.Equal(0, Count("Student"));
    }

    [Theory]
    [InlineData("23456789012")]
    [InlineData("234567890123")]
    [InlineData("034567890124")]
    [InlineData("23456789012a")]
    public void Save_InvalidAadhaar_IsRejected(string aadhaar)
    {
        var ex = Assert.Throws<ValidationException>(() => Add(NewStudent(aadhaar: aadhaar)));

        Assert.Contains("Aadhaar", ex.Message);
    }

    [Fact]
    public void Save_DuplicateAadhaar_IsRejected()
    {
        Add(NewStudent("First", Aadhaar1));

        var ex = Assert.Throws<ValidationException>(() => Add(NewStudent("Second", Aadhaar1)));

        Assert.Contains("already has this Aadhaar", ex.Message);
        Add(NewStudent("Third", Aadhaar2));
        Add(NewStudent("Fourth"));
        Add(NewStudent("Fifth"));
    }

    [Fact]
    public void Save_DatesAreChecked()
    {
        Student future = NewStudent();
        future.DateOfBirth = DateTime.Today.AddDays(1);
        Assert.Contains("past", Assert.Throws<ValidationException>(() => Add(future)).Message);

        Student early = NewStudent();
        early.DateOfBirth = new DateTime(2008, 1, 1);
        early.AdmissionDate = new DateTime(2007, 1, 1);
        Assert.Contains("after the date of birth", Assert.Throws<ValidationException>(() => Add(early)).Message);
    }

    [Fact]
    public void Save_CannotMoveStudentToAnotherHostelsCollege()
    {
        Student student = Add();
        int otherCollege = AddCollege(AddHostel("Other Hostel"), "Other College");

        student.CollegeId = otherCollege;
        var ex = Assert.Throws<ValidationException>(() =>
            StudentService.Save(student, StudentService.GetPrimaryParent(student.StudentId)!));

        Assert.Contains("another hostel", ex.Message);
    }

    [Fact]
    public void Save_StoresPhotoAndAadhaarCardUnderGeneratedNames()
    {
        Student student = Add(files: new StudentFileChanges(CreatePng(), NewAadhaarCardFile: CreatePdf()));

        Assert.StartsWith(Path.Combine("Photos", "Students", $"S{student.StudentId}_photo_"), student.PhotoPath);
        Assert.EndsWith(".png", student.PhotoPath);
        Assert.StartsWith(Path.Combine("Documents", "Students", $"S{student.StudentId}_aadhaar_"), student.AadhaarCardPath);
        Assert.True(StudentFileService.Exists(student.PhotoPath));
        Assert.True(StudentFileService.Exists(student.AadhaarCardPath));

        using Image? photo = StudentFileService.LoadPhoto(student.PhotoPath);
        Assert.NotNull(photo);
    }

    [Fact]
    public void Save_ReplacingOrRemovingFiles_DeletesTheOldFiles()
    {
        Student student = Add(files: new StudentFileChanges(CreatePng("one.png"), NewAadhaarCardFile: CreatePdf()));
        Parent parent = StudentService.GetPrimaryParent(student.StudentId)!;
        string oldPhoto = student.PhotoPath;
        string oldCard = student.AadhaarCardPath;

        Thread.Sleep(5);
        Student updated = StudentService.Save(student, parent,
            new StudentFileChanges(CreatePng("two.png"), RemoveAadhaarCard: true));

        Assert.NotEqual(oldPhoto, updated.PhotoPath);
        Assert.False(StudentFileService.Exists(oldPhoto));
        Assert.True(StudentFileService.Exists(updated.PhotoPath));
        Assert.Equal(string.Empty, updated.AadhaarCardPath);
        Assert.False(StudentFileService.Exists(oldCard));
    }

    [Fact]
    public void Save_InvalidFiles_AreRejectedBeforeAnythingIsSaved()
    {
        string notAnImage = Path.Combine(DataFolder, "fake.png");
        File.WriteAllText(notAnImage, "not an image");
        string notAPdf = Path.Combine(DataFolder, "fake.pdf");
        File.WriteAllText(notAPdf, "not a pdf");
        string wrongType = Path.Combine(DataFolder, "card.docx");
        File.WriteAllText(wrongType, "x");

        Assert.Contains("could not be read", Assert.Throws<ValidationException>(() =>
            Add(files: new StudentFileChanges(notAnImage))).Message);
        Assert.Contains("not a valid PDF", Assert.Throws<ValidationException>(() =>
            Add(files: new StudentFileChanges(NewAadhaarCardFile: notAPdf))).Message);
        Assert.Contains("file types", Assert.Throws<ValidationException>(() =>
            Add(files: new StudentFileChanges(NewAadhaarCardFile: wrongType))).Message);
        Assert.Contains("could not be found", Assert.Throws<ValidationException>(() =>
            Add(files: new StudentFileChanges(Path.Combine(DataFolder, "missing.png")))).Message);

        Assert.Equal(0, Count("Student"));
    }

    [Fact]
    public void Save_FileLargerThan5MB_IsRejected()
    {
        string big = Path.Combine(DataFolder, "big.pdf");
        using (FileStream stream = File.Create(big))
        {
            stream.Write("%PDF"u8);
            stream.SetLength(StudentFileService.MaxFileBytes + 1);
        }

        var ex = Assert.Throws<ValidationException>(() => Add(files: new StudentFileChanges(NewAadhaarCardFile: big)));

        Assert.Contains("5 MB", ex.Message);
    }

    [Fact]
    public void MissingPhotoFile_IsReportedAsMissing()
    {
        Student student = Add(files: new StudentFileChanges(CreatePng()));
        File.Delete(StudentFileService.FullPath(student.PhotoPath));

        Assert.False(StudentFileService.Exists(student.PhotoPath));
        Assert.Null(StudentFileService.LoadPhoto(student.PhotoPath));
    }

    [Fact]
    public void Delete_NewStudent_RemovesStudentParentsAndFiles()
    {
        Student student = Add(files: new StudentFileChanges(CreatePng()));

        StudentService.Delete(student.StudentId);

        Assert.Equal(0, Count("Student"));
        Assert.Equal(0, Count("Parent"));
        Assert.False(StudentFileService.Exists(student.PhotoPath));
    }

    [Fact]
    public void Delete_StudentWithRoomHistory_IsRejected()
    {
        Student student = Add();
        Room room = RoomService.Save(new Room { HostelId = HostelId, RoomNumber = "101", SharingTypeId = SharingTypeId(1), Gender = RoomGender.Male });
        Data.Db.Execute(
            "INSERT INTO [RoomAllocation] ([StudentId], [RoomId], [CheckInDate], [Status]) VALUES (?, ?, ?, ?)",
            Data.Db.Param("@StudentId", student.StudentId), Data.Db.Param("@RoomId", room.RoomId),
            Data.Db.Param("@CheckInDate", DateTime.Today), Data.Db.Param("@Status", AllocationStatus.CheckedOut));

        var ex = Assert.Throws<ValidationException>(() => StudentService.Delete(student.StudentId));

        Assert.Contains("set the status to Left", ex.Message);
        Assert.Equal(1, Count("Student"));
    }

    [Theory]
    [InlineData("234567890124", true)]
    [InlineData("2345 6789 0124", true)]
    [InlineData("987654321012", true)]
    [InlineData("", true)]
    [InlineData("234567890123", false)]
    [InlineData("134567890124", false)]
    [InlineData("23456789012", false)]
    public void Validators_Aadhaar(string text, bool valid) =>
        Assert.Equal(valid, Validators.IsValidAadhaarOrEmpty(Validators.CleanAadhaar(text)));
}
