using HostelManagement.Forms.Views;
using HostelManagement.Models;
using HostelManagement.Services;
using HostelManagement.Utilities;

namespace HostelManagement.Forms;

/// <summary>
/// Application shell: left navigation menu, page header and a content area
/// that hosts one module screen (UserControl) at a time.
/// </summary>
public partial class MainForm : Form
{
    private readonly List<NavigationItem> _navigationItems;
    private readonly Dictionary<NavigationItem, Button> _navButtons = new();
    private readonly ComboBox _hostelSelector;
    private NavigationItem? _currentItem;
    private int? _shownHostelId;

    public MainForm()
    {
        InitializeComponent();
        ApplyTheme();

        _navigationItems = BuildNavigation();
        CreateNavButtons();
        _hostelSelector = CreateHostelSelector();

        dateStatusLabel.Text = DateTime.Today.ToString("dddd, dd MMM yyyy");

        LoadHostelSelector();
        ShowHostelName();
        _shownHostelId = HostelContext.CurrentHostelId;

        HostelService.HostelsChanged += OnHostelsChanged;
        HostelContext.CurrentHostelChanged += OnCurrentHostelChanged;
        Disposed += (_, _) =>
        {
            HostelService.HostelsChanged -= OnHostelsChanged;
            HostelContext.CurrentHostelChanged -= OnCurrentHostelChanged;
        };
    }

    /// <summary>Makes the day's automatic backup the first time the application is closed each day.</summary>
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        if (e.Cancel)
        {
            return;
        }

        Cursor = Cursors.WaitCursor;
        dateStatusLabel.Text = "Making the daily backup...";
        Application.DoEvents();
        try
        {
            BackupResult? result = BackupService.RunAutomaticIfDue(DateTime.Now);
            if (result?.SecondCopyProblem is string problem)
            {
                Dialogs.Warning("The daily backup was made, but: " + problem);
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error("The daily backup failed.", ex);
            Dialogs.Warning("The daily backup could not be made. Please make one on the Backup / Restore screen " +
                            "next time. Technical details were written to the log file.");
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);

        // Without any hostel the admin starts on the Hostels screen to add one.
        Navigate(HostelContext.CurrentHostel is null
            ? _navigationItems.First(item => item.Title == "Hostels")
            : _navigationItems[0]);
    }

    /// <summary>
    /// The application menu. Screens marked as requiring a hostel work on the hostel
    /// selected at the top right.
    /// </summary>
    internal static List<NavigationItem> BuildNavigation() =>
    [
        new("OVERVIEW", "Dashboard", "Summary of students, rooms, payments and dues.",
            hostel => new DashboardView(hostel!), RequiresHostel: true),

        new("HOSTEL", "Hostels", "Add and edit hostels. Select the hostel to work on at the top right.",
            _ => new HostelsView()),
        new("HOSTEL", "Colleges", "Colleges whose students stay in the selected hostel.",
            hostel => new CollegesView(hostel!), RequiresHostel: true),
        new("HOSTEL", "Rooms", "Rent by sharing type, rooms, occupancy and free beds.",
            hostel => new RoomsView(hostel!), RequiresHostel: true),
        new("HOSTEL", "Room Allocation", "Check-in, room transfer and check-out.",
            hostel => new AllocationsView(hostel!), RequiresHostel: true),
        new("HOSTEL", "Services", "Services included in the rent and extra services such as transport.",
            hostel => new ServicesView(hostel!), RequiresHostel: true),

        new("STUDENTS", "Students", "Student records, parents, photos and Aadhaar cards.",
            hostel => new StudentsView(hostel!), RequiresHostel: true),
        new("STUDENTS", "Parents / Guardians", "Parent and guardian contact details.",
            hostel => new ParentsView(hostel!), RequiresHostel: true),

        new("BILLING", "Invoices", "Create, print and export invoices.",
            hostel => new InvoicesView(hostel!), RequiresHostel: true),
        new("BILLING", "Payments", "Record payments against invoices.",
            hostel => new PaymentsView(hostel!), RequiresHostel: true),
        new("BILLING", "Pending Dues", "Outstanding invoice balances and reminders.",
            hostel => new PendingDuesView(hostel!), RequiresHostel: true),
        new("BILLING", "Reports", "Student, room, payment and dues reports.",
            hostel => new ReportsView(hostel!), RequiresHostel: true),

        new("SETTINGS", "Admin Account", "Change the password, and the admin's email and phone.",
            _ => new AdminAccountView()),
        new("SETTINGS", "Email Settings", "Gmail account, email texts and the history of emails sent.",
            _ => new EmailSettingsView()),
        new("SETTINGS", "Backup / Restore", "Back up the database, photos and documents, and restore a backup.",
            _ => new BackupView()),
        new("SETTINGS", "Application Settings", "Database location and database check.",
            _ => new ApplicationSettingsView()),
    ];

    // ---- Hostel selection ----

    private ComboBox CreateHostelSelector()
    {
        var selector = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = UiTheme.BodyFont,
            Width = 260,
            Margin = new Padding(0, 14, 0, 0),
            DisplayMember = nameof(Hostel.HostelName),
            ValueMember = nameof(Hostel.HostelId),
            Name = "hostelSelector",
        };
        selector.SelectionChangeCommitted += (_, _) =>
        {
            if (selector.SelectedValue is int hostelId && hostelId != HostelContext.CurrentHostelId)
            {
                HostelContext.Select(hostelId);
            }
        };

        var caption = new Label
        {
            Text = "Hostel",
            AutoSize = true,
            Font = UiTheme.BodyBoldFont,
            ForeColor = UiTheme.TextPrimary,
            Margin = new Padding(0, 18, 8, 0),
        };

        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            WrapContents = false,
            BackColor = UiTheme.HeaderBackground,
        };
        panel.Controls.Add(caption);
        panel.Controls.Add(selector);

        // Added last so it is docked first and the title labels use the remaining width.
        headerPanel.Controls.Add(panel);
        return selector;
    }

    private void LoadHostelSelector()
    {
        try
        {
            _hostelSelector.DataSource = HostelService.GetHostels();
            _hostelSelector.SelectedValue = HostelContext.CurrentHostelId ?? -1;
            _hostelSelector.Enabled = _hostelSelector.Items.Count > 0;
        }
        catch (Exception ex)
        {
            AppLogger.Error("Could not load the hostel list.", ex);
        }
    }

    private void OnHostelsChanged(object? sender, EventArgs e)
    {
        HostelContext.Refresh();
        LoadHostelSelector();
    }

    private void OnCurrentHostelChanged(object? sender, EventArgs e)
    {
        ShowHostelName();
        if (_hostelSelector.SelectedValue is not int id || id != HostelContext.CurrentHostelId)
        {
            LoadHostelSelector();
        }

        // Screens that show one hostel's data are rebuilt for the newly selected hostel.
        if (_shownHostelId != HostelContext.CurrentHostelId)
        {
            _shownHostelId = HostelContext.CurrentHostelId;
            if (_currentItem is { RequiresHostel: true } item)
            {
                Navigate(item, force: true);
            }
        }
    }

    /// <summary>Shows the selected hostel's name in the title bar and at the top of the menu.</summary>
    private void ShowHostelName()
    {
        string hostelName = HostelContext.CurrentHostel?.HostelName ?? string.Empty;
        bool hasName = hostelName.Length > 0;
        brandLabel.Text = hasName ? hostelName : "Hostel Management";
        Text = hasName
            ? $"{hostelName} | {AppInfo.ProductName} (v{AppInfo.Version})"
            : $"{AppInfo.ProductName} (v{AppInfo.Version})";
    }

    private void ApplyTheme()
    {
        navPanel.BackColor = UiTheme.NavBackground;
        brandPanel.BackColor = UiTheme.NavBackground;
        brandLabel.Font = UiTheme.BrandFont;
        brandLabel.ForeColor = UiTheme.NavSelected;
        AddMenuPhoto();
        brandLabel.AutoEllipsis = true;

        headerPanel.BackColor = UiTheme.HeaderBackground;
        pageTitleLabel.Font = UiTheme.HeadingFont;
        pageTitleLabel.ForeColor = UiTheme.TextPrimary;
        pageDescriptionLabel.Font = UiTheme.SubHeadingFont;
        pageDescriptionLabel.ForeColor = UiTheme.TextMuted;

        contentPanel.BackColor = UiTheme.ContentBackground;
    }

    /// <summary>
    /// The hostel photo at the bottom of the menu, with a thin line between menu and content.
    /// The photo only uses the space the menu does not need, so the menu never needs a scroll bar.
    /// </summary>
    private void AddMenuPhoto()
    {
        Image? photo = AppImages.LoadHostelPhoto();
        if (photo is not null)
        {
            var pictureBox = new PictureBox
            {
                Name = "menuPhoto",
                Dock = DockStyle.Bottom,
                Height = 0,
                SizeMode = PictureBoxSizeMode.Zoom,
                Image = photo,
                BackColor = UiTheme.NavBackground,
            };
            Disposed += (_, _) => photo.Dispose();
            navPanel.Controls.Add(pictureBox);
            navPanel.Resize += (_, _) => FitMenuPhoto(pictureBox, photo);
            Shown += (_, _) => FitMenuPhoto(pictureBox, photo);
        }

        navPanel.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 1, BackColor = UiTheme.Border });
    }

    private void FitMenuPhoto(PictureBox pictureBox, Image photo)
    {
        const int minimumHeight = 70;
        int menuHeight = navButtonsPanel.Padding.Vertical +
            navButtonsPanel.Controls.Cast<Control>().Sum(c => c.Height + c.Margin.Vertical);
        int spare = navPanel.ClientSize.Height - brandPanel.Height - menuHeight - 8;
        int natural = navPanel.ClientSize.Width * photo.Height / photo.Width;
        int height = Math.Min(natural, spare);

        pictureBox.Visible = height >= minimumHeight;
        pictureBox.Height = Math.Max(height, 0);
    }

    private void CreateNavButtons()
    {
        navButtonsPanel.SuspendLayout();

        string? currentGroup = null;
        foreach (NavigationItem item in _navigationItems)
        {
            if (item.Group != currentGroup)
            {
                currentGroup = item.Group;
                navButtonsPanel.Controls.Add(CreateGroupLabel(item.Group));
            }

            Button button = CreateNavButton(item);
            _navButtons[item] = button;
            navButtonsPanel.Controls.Add(button);
        }

        navButtonsPanel.ResumeLayout();
        navButtonsPanel.ClientSizeChanged += (_, _) => FitNavItemsToWidth();
        FitNavItemsToWidth();
    }

    // Keeps menu items full width, also when a vertical scrollbar appears on small screens.
    private void FitNavItemsToWidth()
    {
        int width = navButtonsPanel.ClientSize.Width;
        foreach (Control control in navButtonsPanel.Controls)
        {
            control.Width = width;
        }
    }

    private Label CreateGroupLabel(string text) => new()
    {
        Text = text,
        Font = UiTheme.NavGroupFont,
        ForeColor = UiTheme.NavGroupText,
        Height = 26,
        Padding = new Padding(16, 8, 0, 0),
        Margin = Padding.Empty,
    };

    private Button CreateNavButton(NavigationItem item)
    {
        var button = new Button
        {
            Text = item.Title,
            Font = UiTheme.NavFont,
            ForeColor = UiTheme.NavText,
            BackColor = UiTheme.NavBackground,
            FlatStyle = FlatStyle.Flat,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(16, 0, 0, 0),
            Height = 32,
            Margin = Padding.Empty,
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false,
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = UiTheme.NavHover;
        button.Click += (_, _) => Navigate(item);
        return button;
    }

    private void Navigate(NavigationItem item, bool force = false)
    {
        if (item == _currentItem && !force)
        {
            return;
        }

        Hostel? hostel = HostelContext.CurrentHostel;
        UserControl view;
        try
        {
            view = item.RequiresHostel && hostel is null
                ? new MessageView(item.Title, "There is no hostel yet. Open Hostels in the menu and add a hostel first.")
                : item.CreateView(hostel);
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, $"The {item.Title} screen could not be opened.");
            return;
        }

        contentPanel.SuspendLayout();
        foreach (Control old in contentPanel.Controls.Cast<Control>().ToList())
        {
            contentPanel.Controls.Remove(old);
            old.Dispose();
        }

        view.Dock = DockStyle.Fill;
        contentPanel.Controls.Add(view);
        contentPanel.ResumeLayout();

        pageTitleLabel.Text = item.Title;
        pageDescriptionLabel.Text = item.Description;
        statusLabel.Text = item.Title;
        HighlightNavButton(item);
        _currentItem = item;
    }

    private void HighlightNavButton(NavigationItem selected)
    {
        foreach ((NavigationItem item, Button button) in _navButtons)
        {
            bool isSelected = item == selected;
            button.BackColor = isSelected ? UiTheme.NavSelected : UiTheme.NavBackground;
            button.ForeColor = isSelected ? Color.White : UiTheme.NavText;
            button.FlatAppearance.MouseOverBackColor = isSelected ? UiTheme.NavSelected : UiTheme.NavHover;
        }
    }
}
