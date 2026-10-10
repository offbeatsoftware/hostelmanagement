using System.Drawing.Imaging;
using HostelManagement.Forms;
using HostelManagement.Models;
using HostelManagement.Services;
using Xunit;
using Xunit.Abstractions;

namespace HostelManagement.Tests;

/// <summary>
/// Renders screens to images for a visual check. Does nothing unless the PRINT_SCREENSHOTS
/// environment variable is set; the CI workflow sets it and prints a small JPEG as base64 to the log.
/// </summary>
public sealed class ScreenshotTests : TestDatabase
{
    private readonly ITestOutputHelper _output;

    public ScreenshotTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void LoginScreen()
    {
        if (Environment.GetEnvironmentVariable("PRINT_SCREENSHOTS") is null)
        {
            return;
        }

        AddHostel("Boys Hostel");
        Print("LOGIN", () => new LoginForm(HostelService.GetHostels()));
    }

    [Fact]
    public void MainScreen()
    {
        if (Environment.GetEnvironmentVariable("PRINT_SCREENSHOTS") is null)
        {
            return;
        }

        SeedSampleData();
        HostelContext.Select(HostelId);
        Print("MAIN", () => new MainForm { WindowState = FormWindowState.Normal, Size = new Size(1280, 760) });
    }

    [Fact]
    public void ReportsScreen()
    {
        if (Environment.GetEnvironmentVariable("PRINT_SCREENSHOTS") is null)
        {
            return;
        }

        SeedSampleData();
        HostelContext.Select(HostelId);
        Print("REPORTS", () => new MainForm { WindowState = FormWindowState.Normal, Size = new Size(1280, 760) }, form =>
        {
            Button reports = form.Controls.Find("navButtonsPanel", searchAllChildren: true).Single()
                .Controls.OfType<Button>().Single(b => b.Text == "Reports");
            reports.PerformClick();
            Application.DoEvents();
            AllControls(form).OfType<ListBox>().Single().SelectedIndex = 1; // Room occupancy
            Application.DoEvents();
        });
    }

    private static IEnumerable<Control> AllControls(Control parent) =>
        parent.Controls.Cast<Control>().SelectMany(c => new[] { c }.Concat(AllControls(c)));

    /// <summary>Rooms, students, invoices and payments so the dashboard shows figures, lists and the chart.</summary>
    private void SeedSampleData()
    {
        DateTime today = DateTime.Today;
        DateTime yearStart = AcademicYear.Start(AcademicYear.Current);
        string[] names = ["Aman Sharma", "Ravi Kumar", "Karan Mehta", "Rohit Verma", "Vikas Jain", "Sahil Gupta", "Arjun Singh"];
        for (int i = 0; i < names.Length; i++)
        {
            Room room = i % 3 == 2
                ? RoomService.GetRooms(HostelId).FirstOrDefault(r => r.Capacity == 3 && r.Available > 0)
                  ?? RoomService.Save(new Room { HostelId = HostelId, RoomNumber = $"20{i}", SharingTypeId = SharingTypeId(3), Gender = "Male" })
                : RoomService.GetRooms(HostelId).FirstOrDefault(r => r.Capacity == 2 && r.Available > 0)
                  ?? RoomService.Save(new Room { HostelId = HostelId, RoomNumber = $"10{i}", SharingTypeId = SharingTypeId(2), Gender = "Male" });
            DateTime checkIn = yearStart.AddDays(i * 3);
            if (checkIn > today)
            {
                checkIn = today;
            }
            int student = AddStudentWithParents(names[i], admission: checkIn);
            // Every student has his own fee; some use transport.
            Invoice invoice = CheckInWithFee(student, room, 45_000m + 2_500m * i, i % 2 == 0 ? 12_000m : 0m, checkIn);

            // Students pay in parts over the months; the last two have not paid yet.
            for (int month = 0; i < names.Length - 2 && month <= i % 4; month++)
            {
                DateTime paid = yearStart.AddMonths(month).AddDays(10 + i);
                if (paid <= today)
                {
                    Pay(invoice.InvoiceId, 9_000m + 1_500m * i, paid);
                }
            }
        }
    }

    /// <summary>Shows the form, renders it to a small JPEG and writes it to the test output as base64.</summary>
    private void Print(string name, Func<Form> createForm, Action<Form>? afterShow = null)
    {
        string? base64 = null;
        UiThread.Run(() =>
        {
            using Form form = createForm();
            form.Show();
            Application.DoEvents();
            afterShow?.Invoke(form);

            using var full = new Bitmap(form.ClientSize.Width, form.ClientSize.Height);
            form.DrawToBitmap(full, new Rectangle(Point.Empty, full.Size));
            using var small = new Bitmap(full, new Size(full.Width * 3 / 5, full.Height * 3 / 5));
            using var stream = new MemoryStream();
            small.Save(stream, ImageFormat.Jpeg);
            base64 = Convert.ToBase64String(stream.ToArray());
            form.Close();
        });

        _output.WriteLine($"SCREENSHOT-{name}-BEGIN{base64}SCREENSHOT-{name}-END");
    }
}
