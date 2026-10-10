using System.Drawing.Imaging;
using HostelManagement.Forms;
using HostelManagement.Models;
using HostelManagement.Reports;
using HostelManagement.Services;
using PdfSharp.Pdf.IO;
using Xunit;

namespace HostelManagement.Tests;

/// <summary>Student and transport I-cards, and the webcam window for the student photo.</summary>
public sealed class IdCardServiceTests : TestDatabase
{
    private string CreatePhoto(string name = "photo.jpg")
    {
        string file = Path.Combine(DataFolder, name);
        using var bitmap = new Bitmap(300, 400);
        using (Graphics g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.SteelBlue);
        }
        bitmap.Save(file, ImageFormat.Jpeg);
        return file;
    }

    private int StudentWithPhoto(string name, decimal transport)
    {
        int id = AddStudentWithParents(name);
        Student student = StudentService.GetStudent(id)!;
        StudentService.Save(student, new StudentFileChanges(CreatePhoto($"{name}.jpg")));
        CheckInWithFee(id, AddRoom($"{100 + Count("Room") + 1}"), 50_000m, transport);
        return id;
    }

    [Fact]
    public void StudentCard_HasTheStudentFatherRoomAndPhoto_ValidTillTheEndOfTheAcademicYear()
    {
        int aman = StudentWithPhoto("Aman", 0m);

        (List<IdCard> cards, List<string> problems) = IdCardService.Prepare([aman], transport: false, DateTime.Today);

        Assert.Empty(problems);
        IdCard card = Assert.Single(cards);
        Assert.Equal("Aman", card.StudentName);
        Assert.Equal("Rakesh Aman", card.FatherName);
        Assert.Equal("9812345678", card.FatherMobile);
        Assert.Equal("Test College", card.CollegeName);
        Assert.Equal("Room 101, bed 1", card.RoomText);
        Assert.True(File.Exists(card.PhotoFile));
        Assert.Equal(AcademicYear.End(AcademicYear.Current), card.ValidTill);
    }

    [Fact]
    public void TransportCard_OnlyForStudentsWithTransportInThisYearsFee()
    {
        int withTransport = StudentWithPhoto("Aman", 12_000m);
        int without = StudentWithPhoto("Ravi", 0m);

        (List<IdCard> cards, List<string> problems) = IdCardService.Prepare([withTransport, without], transport: true, DateTime.Today);

        Assert.Equal(["Aman"], cards.Select(c => c.StudentName));
        Assert.Contains("Ravi: no transport", Assert.Single(problems));
    }

    [Fact]
    public void Cards_StudentsWhoLeftAreSkipped_AndAMissingPhotoIsReported()
    {
        int left = StudentWithPhoto("Left", 0m);
        AllocationService.CheckOut(left, DateTime.Today);
        int noPhoto = AddStudentWithParents("No Photo");

        (List<IdCard> cards, List<string> problems) = IdCardService.Prepare([left, noPhoto], transport: false, DateTime.Today);

        Assert.Equal(["No Photo"], cards.Select(c => c.StudentName));
        Assert.Null(cards[0].PhotoFile);
        Assert.Equal(["Left: has left the hostel.", "No Photo: no photo (the card has an empty photo box). Add one on the Students screen."],
            problems);
        Assert.Contains("select one or more", Assert.Throws<ValidationException>(() =>
            IdCardService.Prepare([], transport: false, DateTime.Today)).Message);
    }

    [Fact]
    public void Pdf_PrintsEightCardsPerA4Page()
    {
        List<int> students = Enumerable.Range(1, 9).Select(i =>
        {
            int id = AddStudentWithParents($"Student {i}");
            if (i == 1)
            {
                StudentService.Save(StudentService.GetStudent(id)!, new StudentFileChanges(CreatePhoto()));
            }
            return id;
        }).ToList();
        (List<IdCard> cards, _) = IdCardService.Prepare(students, transport: false, DateTime.Today);
        string path = Path.Combine(DataFolder, "Reports", IdCardService.FileName(transport: false, DateTime.Today));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        IdCardPdfWriter.Write(cards, transport: false, path);
        string transportPath = Path.Combine(DataFolder, "Reports", "transport.pdf");
        IdCardPdfWriter.Write(cards.Take(1).ToList(), transport: true, transportPath);

        using PdfSharp.Pdf.PdfDocument pdf = PdfReader.Open(path, PdfDocumentOpenMode.Import);
        Assert.Equal(2, pdf.PageCount);
        Assert.True(new FileInfo(transportPath).Length > 1000);
        Assert.StartsWith("ICards_", Path.GetFileName(path));
        Assert.StartsWith("TransportCards_", IdCardService.FileName(transport: true, DateTime.Today));
    }

    [Fact]
    public void CameraWindow_OpensAndExplainsWhenNoCameraCanBeUsed()
    {
        MainFormTests.RunOnStaThread(() =>
        {
            using var camera = new CameraForm();
            camera.Show();
            Label status = camera.Controls.OfType<Label>().Single(l => l.Text.StartsWith("Starting", StringComparison.Ordinal));
            Button capture = camera.Controls.OfType<Button>().Single(b => b.Text == "Capture");

            // The build computers have no camera: the window must say so instead of failing.
            DateTime until = DateTime.Now.AddSeconds(15);
            while (status.Text.StartsWith("Starting", StringComparison.Ordinal) && DateTime.Now < until)
            {
                Application.DoEvents();
                Thread.Sleep(20);
            }

            Assert.False(status.Text.StartsWith("Starting", StringComparison.Ordinal), "The camera window kept waiting.");
            if (!capture.Enabled)
            {
                Assert.Contains("camera", status.Text, StringComparison.OrdinalIgnoreCase);
            }
            Assert.Null(camera.PhotoFile);
            camera.Close();
            Application.DoEvents();
        });
    }
}
