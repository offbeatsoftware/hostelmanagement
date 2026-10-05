using System.Drawing.Imaging;
using HostelManagement.Forms;
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

        HostelContext.Select(HostelId);
        Print("MAIN", () => new MainForm { WindowState = FormWindowState.Normal, Size = new Size(1280, 760) });
    }

    /// <summary>Shows the form, renders it to a small JPEG and writes it to the test output as base64.</summary>
    private void Print(string name, Func<Form> createForm)
    {
        string? base64 = null;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                using Form form = createForm();
                form.Show();
                Application.DoEvents();

                using var full = new Bitmap(form.ClientSize.Width, form.ClientSize.Height);
                form.DrawToBitmap(full, new Rectangle(Point.Empty, full.Size));
                using var small = new Bitmap(full, new Size(full.Width * 3 / 5, full.Height * 3 / 5));
                using var stream = new MemoryStream();
                small.Save(stream, ImageFormat.Jpeg);
                base64 = Convert.ToBase64String(stream.ToArray());
                form.Close();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(failure);
        _output.WriteLine($"SCREENSHOT-{name}-BEGIN{base64}SCREENSHOT-{name}-END");
    }
}
