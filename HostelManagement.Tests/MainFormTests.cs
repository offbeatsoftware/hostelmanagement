using HostelManagement.Forms;
using HostelManagement.Models;
using HostelManagement.Services;
using Xunit;

namespace HostelManagement.Tests;

public sealed class MainFormTests : TestDatabase
{
    [Fact]
    public void Navigation_ContainsAllFourteenScreens()
    {
        string[] expected =
        [
            "Dashboard", "Hostel Details", "Rooms", "Room Allocation", "Services", "Students",
            "Parents / Guardians", "Invoices", "Payments", "Pending Dues", "Reports",
            "Email Settings", "Backup / Restore", "Application Settings",
        ];

        Assert.Equal(expected, MainForm.BuildNavigation().Select(item => item.Title));
    }

    [Fact]
    public void Navigation_EveryScreenCanBeCreated()
    {
        RunOnStaThread(() =>
        {
            foreach (NavigationItem item in MainForm.BuildNavigation())
            {
                using UserControl view = item.CreateView();
                Assert.NotNull(view);
            }
        });
    }

    [Fact]
    public void MainForm_OpensEveryScreenFromTheMenu()
    {
        RunOnStaThread(() =>
        {
            using var form = new MainForm { WindowState = FormWindowState.Normal };
            form.Show();
            Application.DoEvents();

            Control content = form.Controls.Find("contentPanel", searchAllChildren: true).Single();
            Control title = form.Controls.Find("pageTitleLabel", searchAllChildren: true).Single();
            Control menu = form.Controls.Find("navButtonsPanel", searchAllChildren: true).Single();

            // The Dashboard opens on start.
            Assert.Equal("Dashboard", title.Text);
            Assert.Single(content.Controls);

            List<Button> buttons = menu.Controls.OfType<Button>().ToList();
            Assert.Equal(14, buttons.Count);

            foreach (Button button in buttons)
            {
                button.PerformClick();
                Application.DoEvents();

                Assert.Equal(button.Text, title.Text);
                Assert.Single(content.Controls);
            }

            form.Close();
        });
    }

    [Fact]
    public void MainForm_ShowsHostelNameAndUpdatesItAfterSave()
    {
        HostelService.Save(new HostelDetails { HostelName = "Green Valley Hostel" });

        RunOnStaThread(() =>
        {
            using var form = new MainForm();
            Control brand = form.Controls.Find("brandLabel", searchAllChildren: true).Single();

            Assert.StartsWith("Green Valley Hostel", form.Text);
            Assert.Equal("Green Valley Hostel", brand.Text);

            HostelService.Save(new HostelDetails { HostelName = "Blue Hills Hostel" });

            Assert.StartsWith("Blue Hills Hostel", form.Text);
            Assert.Equal("Blue Hills Hostel", brand.Text);
        });
    }

    [Fact]
    public void LoginForm_CanBeCreated()
    {
        RunOnStaThread(() =>
        {
            using var form = new LoginForm("Green Valley Hostel");
            Assert.Equal("Sign in", form.Text);
        });
    }

    private static void RunOnStaThread(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();

        if (!thread.Join(TimeSpan.FromMinutes(2)))
        {
            throw new TimeoutException("The UI test did not finish (a message box may be open).");
        }
        if (failure is not null)
        {
            throw new Xunit.Sdk.XunitException($"UI test failed: {failure}");
        }
    }
}
