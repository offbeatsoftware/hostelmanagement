using HostelManagement.Models;
using HostelManagement.Services;
using HostelManagement.Utilities;

namespace HostelManagement.Forms;

/// <summary>
/// Admin sign in shown before the main window, with the hostel to work on. The hostel photo
/// fills the window and the sign in box sits on the right.
/// DialogResult.OK means signed in; <see cref="SelectedHostelId"/> is the chosen hostel (null when none exist).
/// </summary>
public sealed class LoginForm : Form
{
    private readonly TextBox _userNameBox;
    private readonly TextBox _passwordBox;
    private readonly ComboBox _hostelBox;
    private readonly Label _messageLabel;
    private readonly Image? _background;

    public LoginForm(IReadOnlyList<Hostel> hostels)
    {
        Text = "Sign in";
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Font;
        Font = UiTheme.BodyFont;
        BackColor = UiTheme.NavBackground;
        ClientSize = new Size(1000, 620);
        DoubleBuffered = true;

        _background = LoadBackground();

        // ---- Sign in box ----
        var card = new Panel
        {
            BackColor = Color.White,
            Size = new Size(400, 470),
            Location = new Point(560, 75),
            Padding = new Padding(32, 28, 32, 24),
        };

        var titleLabel = new Label
        {
            AutoSize = false,
            Size = new Size(336, 34),
            Location = new Point(32, 28),
            Font = UiTheme.HeadingFont,
            ForeColor = UiTheme.TextPrimary,
            Text = AppInfo.ProductName,
            AutoEllipsis = true,
        };
        var subtitleLabel = new Label
        {
            AutoSize = true,
            Location = new Point(34, 66),
            Font = UiTheme.BodyFont,
            ForeColor = UiTheme.TextMuted,
            Text = "Sign in to continue",
        };

        TableLayoutPanel fields = FormFields.CreateTable(labelWidth: 96, inputWidth: 240);
        _userNameBox = FormFields.AddTextBox(fields, "User name", 50);
        _passwordBox = FormFields.AddTextBox(fields, "Password", 50);
        _passwordBox.UseSystemPasswordChar = true;

        _hostelBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = UiTheme.BodyFont,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 4, 0, 4),
            DisplayMember = nameof(Hostel.HostelName),
            ValueMember = nameof(Hostel.HostelId),
            DataSource = hostels.ToList(),
            TabIndex = fields.Controls.Count,
        };
        if (hostels.Count > 0)
        {
            FormFields.AddRow(fields, "Hostel", _hostelBox);
        }
        fields.Location = new Point(32, 115);

        _messageLabel = FormFields.CreateMessageLabel();
        _messageLabel.Location = new Point(32, 250);
        _messageLabel.Margin = Padding.Empty;
        _messageLabel.MaximumSize = new Size(336, 0);

        var signInButton = new Button { Text = "Sign in", TabIndex = 2, Location = new Point(32, 300) };
        UiTheme.StylePrimaryButton(signInButton);
        signInButton.Size = new Size(336, 40);
        signInButton.Click += (_, _) => SignIn();

        var cancelButton = new Button { Text = "Exit", TabIndex = 3, Location = new Point(32, 352) };
        UiTheme.StyleSecondaryButton(cancelButton);
        cancelButton.Size = new Size(336, 40);
        cancelButton.DialogResult = DialogResult.Cancel;

        var versionLabel = new Label
        {
            AutoSize = true,
            Location = new Point(34, 425),
            Font = UiTheme.NavGroupFont,
            ForeColor = UiTheme.TextMuted,
            Text = $"Version {AppInfo.Version}",
        };

        card.Controls.AddRange([titleLabel, subtitleLabel, fields, _messageLabel, signInButton, cancelButton, versionLabel]);
        Controls.Add(card);

        AcceptButton = signInButton;
        CancelButton = cancelButton;

        Shown += (_, _) => _userNameBox.Focus();
        FormClosed += (_, _) => _background?.Dispose();
    }

    /// <summary>True when the background photo was loaded (checked by the automated tests).</summary>
    internal bool HasBackgroundPhoto => _background is not null;

    /// <summary>The hostel chosen on the sign in screen, or null when no hostel exists yet.</summary>
    public int? SelectedHostelId => _hostelBox.SelectedValue is int id ? id : null;

    /// <summary>Draws the photo so it covers the whole window without being stretched.</summary>
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        if (_background is null)
        {
            base.OnPaintBackground(e);
            return;
        }

        float scale = Math.Max((float)ClientSize.Width / _background.Width, (float)ClientSize.Height / _background.Height);
        float width = _background.Width * scale;
        float height = _background.Height * scale;

        // Keep the left part of the photo (the sign board) visible; the sign in box covers the right.
        float x = 0;
        float y = (ClientSize.Height - height) / 2;
        e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        e.Graphics.DrawImage(_background, x, y, width, height);
    }

    private static Image? LoadBackground()
    {
        try
        {
            using Stream? stream = typeof(LoginForm).Assembly.GetManifestResourceStream("HostelManagement.LoginBackground.jpg");
            if (stream is null)
            {
                return null;
            }
            using var image = Image.FromStream(stream);
            return new Bitmap(image);
        }
        catch (Exception ex) when (ex is ArgumentException or OutOfMemoryException or IOException)
        {
            AppLogger.Error("Could not load the sign in background photo.", ex);
            return null;
        }
    }

    private void SignIn()
    {
        if (AuthService.IsValidLogin(_userNameBox.Text, _passwordBox.Text))
        {
            AppLogger.Info("Admin signed in.");
            DialogResult = DialogResult.OK;
            Close();
            return;
        }

        AppLogger.Info("Failed sign in attempt.");
        FormFields.ShowError(_messageLabel, "Incorrect user name or password.");
        _passwordBox.Clear();
        _passwordBox.Focus();
    }
}
