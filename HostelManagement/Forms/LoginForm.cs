using System.Drawing.Drawing2D;
using HostelManagement.Models;
using HostelManagement.Services;
using HostelManagement.Utilities;

namespace HostelManagement.Forms;

/// <summary>
/// Admin sign in shown before the main window, with the hostel to work on. The hostel photo
/// fills the window; the sign in panel sits at the bottom right.
/// DialogResult.OK means signed in; <see cref="SelectedHostelId"/> is the chosen hostel (null when none exist).
/// </summary>
public sealed class LoginForm : Form
{
    // Colours taken from the photo: night sky, lit sign board and the terracotta facade.
    private static readonly Color PanelColor = Color.FromArgb(205, 24, 27, 33);
    private static readonly Color Gold = Color.FromArgb(232, 186, 98);
    private static readonly Color Terracotta = Color.FromArgb(192, 101, 43);
    private static readonly Color TerracottaDark = Color.FromArgb(160, 80, 32);
    private static readonly Color LightText = Color.FromArgb(236, 230, 218);
    private static readonly Color MutedText = Color.FromArgb(170, 165, 155);
    private static readonly Color ErrorText = Color.FromArgb(255, 140, 120);

    // Bottom right of the window, over the driveway and the right end of the building.
    private const int PanelWidth = 420;
    private const int PanelHeight = 430;
    private const int WindowWidth = 1100;
    private const int WindowHeight = 650;
    private static readonly Rectangle PanelBounds =
        new(WindowWidth - PanelWidth - 36, WindowHeight - PanelHeight - 30, PanelWidth, PanelHeight);
    private static readonly int InnerLeft = PanelBounds.Left + 30;
    private const int InnerWidth = PanelWidth - 60;

    /// <summary>Vertical position inside the panel.</summary>
    private static int Y(int offset) => PanelBounds.Top + offset;

    private readonly TextBox _userNameBox;
    private readonly TextBox _passwordBox;
    private readonly ComboBox _hostelBox;
    private readonly Label _messageLabel;
    private readonly Image? _background;
    private readonly Font _titleFont = new("Georgia", 21f, FontStyle.Bold);

    public LoginForm(IReadOnlyList<Hostel> hostels)
    {
        Text = "Sign in";
        Icon = AppImages.AppIcon ?? Icon;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Font;
        Font = UiTheme.BodyFont;
        BackColor = Color.FromArgb(24, 27, 33);
        ClientSize = new Size(WindowWidth, WindowHeight);
        DoubleBuffered = true;

        _background = AppImages.LoadHostelPhoto();

        var titleLabel = CreateLabel(AppInfo.BusinessName, new Point(InnerLeft - 2, Y(22)),
            _titleFont, Gold);

        _userNameBox = CreateTextBox(Y(116), tabIndex: 0);
        _passwordBox = CreateTextBox(Y(178), tabIndex: 1);
        _passwordBox.UseSystemPasswordChar = true;

        _hostelBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat,
            Font = UiTheme.BodyFont,
            Location = new Point(InnerLeft, Y(240)),
            Width = InnerWidth,
            DisplayMember = nameof(Hostel.HostelName),
            ValueMember = nameof(Hostel.HostelId),
            DataSource = hostels.ToList(),
            TabIndex = 2,
            Visible = hostels.Count > 0,
        };

        _messageLabel = CreateLabel(string.Empty, new Point(InnerLeft, Y(278)), UiTheme.BodyFont, ErrorText);
        _messageLabel.MaximumSize = new Size(InnerWidth, 0);

        var signInButton = new Button
        {
            Text = "Sign in",
            Location = new Point(InnerLeft, Y(304)),
            Size = new Size(InnerWidth, 40),
            FlatStyle = FlatStyle.Flat,
            BackColor = Terracotta,
            ForeColor = Color.White,
            Font = UiTheme.BodyBoldFont,
            Cursor = Cursors.Hand,
            TabIndex = 3,
            UseVisualStyleBackColor = false,
        };
        signInButton.FlatAppearance.BorderSize = 0;
        signInButton.FlatAppearance.MouseOverBackColor = TerracottaDark;
        signInButton.Click += (_, _) => SignIn();

        var exitButton = new Button
        {
            Text = "Exit",
            Location = new Point(InnerLeft, Y(352)),
            Size = new Size(InnerWidth, 36),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(24, 27, 33),
            ForeColor = LightText,
            Font = UiTheme.BodyFont,
            Cursor = Cursors.Hand,
            TabIndex = 4,
            DialogResult = DialogResult.Cancel,
            UseVisualStyleBackColor = false,
        };
        exitButton.FlatAppearance.BorderColor = MutedText;
        exitButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(45, 49, 58);

        Controls.AddRange(
        [
            titleLabel,
            CreateLabel("User name", new Point(InnerLeft, Y(94)), UiTheme.BodyFont, LightText), _userNameBox,
            CreateLabel("Password", new Point(InnerLeft, Y(156)), UiTheme.BodyFont, LightText), _passwordBox,
            _messageLabel, signInButton, exitButton,
            CreateLabel($"Version {AppInfo.Version}", new Point(InnerLeft, Y(402)), UiTheme.NavGroupFont, MutedText),
        ]);
        if (hostels.Count > 0)
        {
            Controls.Add(CreateLabel("Hostel", new Point(InnerLeft, Y(218)), UiTheme.BodyFont, LightText));
            Controls.Add(_hostelBox);
        }

        AcceptButton = signInButton;
        CancelButton = exitButton;

        Load += (_, _) => KeepPanelInBottomRightCorner();
        Shown += (_, _) => _userNameBox.Focus();
        Disposed += (_, _) =>
        {
            _background?.Dispose();
            _titleFont.Dispose();
        };
    }

    /// <summary>The sign in panel's position; moves when Windows makes the window smaller than designed.</summary>
    private Rectangle _panelBounds = PanelBounds;

    /// <summary>
    /// On a small screen Windows shrinks the window; move the panel and its controls so the panel
    /// stays fully visible in the bottom right corner.
    /// </summary>
    private void KeepPanelInBottomRightCorner()
    {
        int dx = Math.Min(0, ClientSize.Width - WindowWidth);
        int dy = Math.Min(0, ClientSize.Height - WindowHeight);
        if (dx == 0 && dy == 0)
        {
            return;
        }

        foreach (Control control in Controls)
        {
            control.Left += dx;
            control.Top += dy;
        }
        _panelBounds.Offset(dx, dy);
        Invalidate();
    }

    /// <summary>True when the background photo was loaded (checked by the automated tests).</summary>
    internal bool HasBackgroundPhoto => _background is not null;

    /// <summary>The hostel chosen on the sign in screen, or null when no hostel exists yet.</summary>
    public int? SelectedHostelId => _hostelBox.SelectedValue is int id ? id : null;

    /// <summary>
    /// Draws the photo so it covers the window without being stretched, then the see-through
    /// sign in panel with a gold edge and a line under the title.
    /// </summary>
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        base.OnPaintBackground(e);
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;

        if (_background is not null)
        {
            float scale = Math.Max((float)ClientSize.Width / _background.Width, (float)ClientSize.Height / _background.Height);
            float width = _background.Width * scale;
            float height = _background.Height * scale;
            g.DrawImage(_background, (ClientSize.Width - width) / 2, (ClientSize.Height - height) / 2, width, height);
        }

        using GraphicsPath panel = RoundedRectangle(_panelBounds, 14);
        using (var fill = new SolidBrush(PanelColor))
        {
            g.FillPath(fill, panel);
        }
        using (var edge = new Pen(Color.FromArgb(120, Gold), 1f))
        {
            g.DrawPath(edge, panel);
        }
        using (var line = new Pen(Gold, 2f))
        {
            int lineY = _panelBounds.Top + 72;
            int lineX = _panelBounds.Left + 30;
            g.DrawLine(line, lineX, lineY, lineX + 60, lineY);
        }
    }

    private static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
    {
        int diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static Label CreateLabel(string text, Point location, Font font, Color color) => new()
    {
        Text = text,
        AutoSize = true,
        Location = location,
        Font = font,
        ForeColor = color,
        BackColor = Color.Transparent,
    };

    private static TextBox CreateTextBox(int top, int tabIndex) => new()
    {
        Location = new Point(InnerLeft, top),
        Width = InnerWidth,
        Font = UiTheme.BodyFont,
        BorderStyle = BorderStyle.FixedSingle,
        MaxLength = 50,
        TabIndex = tabIndex,
    };

    private void SignIn()
    {
        bool valid;
        try
        {
            valid = AuthService.IsValidLogin(_userNameBox.Text, _passwordBox.Text);
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The sign in could not be checked against the database.");
            return;
        }

        if (valid)
        {
            AppLogger.Info("Admin signed in.");
            DialogResult = DialogResult.OK;
            Close();
            return;
        }

        AppLogger.Info("Failed sign in attempt.");
        _messageLabel.Text = "Incorrect user name or password.";
        _passwordBox.Clear();
        _passwordBox.Focus();
    }
}
