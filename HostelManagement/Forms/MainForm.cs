using HostelManagement.Forms.Views;
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
    private NavigationItem? _currentItem;

    public MainForm()
    {
        InitializeComponent();
        ApplyTheme();

        _navigationItems = BuildNavigation();
        CreateNavButtons();

        Text = $"{AppInfo.ProductName}  (v{AppInfo.Version})";
        dateStatusLabel.Text = DateTime.Today.ToString("dddd, dd MMM yyyy");
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Navigate(_navigationItems[0]);
    }

    /// <summary>
    /// The application menu. Each module replaces its PlaceholderView with a real
    /// screen when it is implemented in its phase.
    /// </summary>
    internal static List<NavigationItem> BuildNavigation() =>
    [
        new("OVERVIEW", "Dashboard", "Summary of students, rooms, payments and dues.",
            () => new PlaceholderView("Dashboard", "Phase 12")),

        new("HOSTEL", "Hostel Details", "Hostel, college and contact information.",
            () => new PlaceholderView("Hostel Details", "Phase 3")),
        new("HOSTEL", "Rooms", "Rooms, sharing types, capacity and rent.",
            () => new PlaceholderView("Rooms", "Phase 4")),
        new("HOSTEL", "Room Allocation", "Check-in, room transfer and check-out.",
            () => new PlaceholderView("Room Allocation", "Phase 6")),
        new("HOSTEL", "Services", "Additional services such as Wi-Fi and transport.",
            () => new PlaceholderView("Services", "Phase 7")),

        new("STUDENTS", "Students", "Student records, photos and search.",
            () => new PlaceholderView("Students", "Phase 5")),
        new("STUDENTS", "Parents / Guardians", "Parent and guardian contact details.",
            () => new PlaceholderView("Parents / Guardians", "Phase 5")),

        new("BILLING", "Invoices", "Create, print and export invoices.",
            () => new PlaceholderView("Invoices", "Phase 8")),
        new("BILLING", "Payments", "Record payments against invoices.",
            () => new PlaceholderView("Payments", "Phase 9")),
        new("BILLING", "Pending Dues", "Outstanding invoice balances and reminders.",
            () => new PlaceholderView("Pending Dues", "Phase 10")),
        new("BILLING", "Reports", "Student, room, payment and dues reports.",
            () => new PlaceholderView("Reports", "Phase 13")),

        new("SETTINGS", "Email Settings", "SMTP configuration for invoices and reminders.",
            () => new PlaceholderView("Email Settings", "Phase 11")),
        new("SETTINGS", "Backup / Restore", "Back up and restore the database.",
            () => new PlaceholderView("Backup / Restore", "Phase 14")),
        new("SETTINGS", "Application Settings", "Database location and database check.",
            () => new ApplicationSettingsView()),
    ];

    private void ApplyTheme()
    {
        navPanel.BackColor = UiTheme.NavBackground;
        brandPanel.BackColor = UiTheme.NavBackground;
        brandLabel.Font = UiTheme.BrandFont;
        brandLabel.ForeColor = Color.White;

        headerPanel.BackColor = UiTheme.HeaderBackground;
        pageTitleLabel.Font = UiTheme.HeadingFont;
        pageTitleLabel.ForeColor = UiTheme.TextPrimary;
        pageDescriptionLabel.Font = UiTheme.SubHeadingFont;
        pageDescriptionLabel.ForeColor = UiTheme.TextMuted;

        contentPanel.BackColor = UiTheme.ContentBackground;
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
        Height = 30,
        Padding = new Padding(16, 10, 0, 0),
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
            Height = 36,
            Margin = Padding.Empty,
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false,
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = UiTheme.NavHover;
        button.Click += (_, _) => Navigate(item);
        return button;
    }

    private void Navigate(NavigationItem item)
    {
        if (item == _currentItem)
        {
            return;
        }

        UserControl view;
        try
        {
            view = item.CreateView();
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
