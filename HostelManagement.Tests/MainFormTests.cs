using HostelManagement.Forms;
using HostelManagement.Models;
using HostelManagement.Services;
using Xunit;

namespace HostelManagement.Tests;

public sealed class MainFormTests : TestDatabase
{
    [Fact]
    public void Navigation_ContainsAllScreens()
    {
        string[] expected =
        [
            "Dashboard", "Hostels", "Colleges", "Rooms", "Room Allocation", "Services", "Students",
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
            Hostel hostel = HostelService.GetHostel(HostelId)!;
            foreach (NavigationItem item in MainForm.BuildNavigation())
            {
                using UserControl view = item.CreateView(hostel);
                Assert.NotNull(view);
            }
        });
    }

    [Fact]
    public void MainForm_WithoutHostels_StartsOnHostelsScreen()
    {
        HostelContext.Select(null);

        RunOnStaThread(() =>
        {
            using var form = new MainForm { WindowState = FormWindowState.Normal };
            form.Show();
            Application.DoEvents();

            Control title = form.Controls.Find("pageTitleLabel", searchAllChildren: true).Single();
            Assert.Equal("Hostels", title.Text);
            form.Close();
        });
    }

    [Fact]
    public void MainForm_OpensEveryScreenFromTheMenu()
    {
        HostelContext.Select(HostelId);

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
            Assert.Equal(15, buttons.Count);

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
    public void MainForm_ShowsSelectedHostelAndFollowsChanges()
    {
        int boys = AddHostel("Boys Hostel");
        int girls = AddHostel("Girls Hostel");
        HostelContext.Select(boys);

        RunOnStaThread(() =>
        {
            using var form = new MainForm();
            Control brand = form.Controls.Find("brandLabel", searchAllChildren: true).Single();
            var selector = (ComboBox)form.Controls.Find("hostelSelector", searchAllChildren: true).Single();

            Assert.StartsWith("Boys Hostel", form.Text);
            Assert.Equal("Boys Hostel", brand.Text);
            Assert.Equal(2, selector.Items.Count);

            HostelContext.Select(girls);
            Assert.StartsWith("Girls Hostel", form.Text);
            Assert.Equal(girls, selector.SelectedValue);

            HostelService.Save(new Hostel { HostelId = girls, HostelName = "Girls Hostel North" });
            Assert.Equal("Girls Hostel North", brand.Text);
        });
    }

    [Fact]
    public void StudentsAndParentsScreens_ShowTheHostelsData()
    {
        StudentService.Save(
            new Student { StudentName = "Aman", Mobile = "9876543210", CollegeId = CollegeId, AdmissionDate = DateTime.Today },
            new Parent { ParentName = "Rakesh", Mobile = "9812345678", Email = "rakesh@example.com" });
        Hostel hostel = HostelService.GetHostel(HostelId)!;

        RunOnStaThread(() =>
        {
            foreach (UserControl view in new UserControl[] { new Forms.Views.StudentsView(hostel), new Forms.Views.ParentsView(hostel) })
            {
                using var host = new Form { Width = 1200, Height = 700 };
                host.Controls.Add(view);
                host.Show();
                Application.DoEvents();

                DataGridView grid = FindGrid(view);
                Assert.Equal(1, grid.Rows.Count);
                host.Close();
            }
        });
    }

    private static DataGridView FindGrid(Control parent) =>
        FindGridOrNull(parent) ?? throw new InvalidOperationException("No grid on the screen.");

    private static DataGridView? FindGridOrNull(Control parent) =>
        parent.Controls.OfType<DataGridView>().FirstOrDefault()
        ?? parent.Controls.Cast<Control>().Select(FindGridOrNull).FirstOrDefault(g => g is not null);

    [Fact]
    public void LoginForm_CanBeCreated()
    {
        RunOnStaThread(() =>
        {
            using var form = new LoginForm(HostelService.GetHostels());
            Assert.Equal("Sign in", form.Text);
            Assert.True(form.HasBackgroundPhoto);
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
