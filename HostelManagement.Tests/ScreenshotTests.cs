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
        string? base64 = null;
        var thread = new Thread(() =>
        {
            using var form = new LoginForm(HostelService.GetHostels());
            form.Show();
            Application.DoEvents();

            using var full = new Bitmap(form.ClientSize.Width, form.ClientSize.Height);
            form.DrawToBitmap(full, new Rectangle(Point.Empty, full.Size));
            using var small = new Bitmap(full, new Size(full.Width * 3 / 5, full.Height * 3 / 5));
            using var stream = new MemoryStream();
            small.Save(stream, ImageFormat.Jpeg);
            base64 = Convert.ToBase64String(stream.ToArray());
            form.Close();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        _output.WriteLine("SCREENSHOT-LOGIN-BEGIN" + base64 + "SCREENSHOT-LOGIN-END");
    }
}
