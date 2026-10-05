using HostelManagement.Models;
using HostelManagement.Services;
using HostelManagement.Utilities;

namespace HostelManagement.Forms;

/// <summary>
/// Admin sign in shown before the main window, with the hostel to work on.
/// DialogResult.OK means signed in; <see cref="SelectedHostelId"/> is the chosen hostel (null when none exist).
/// </summary>
public sealed class LoginForm : Form
{
    private readonly TextBox _userNameBox;
    private readonly TextBox _passwordBox;
    private readonly ComboBox _hostelBox;
    private readonly Label _messageLabel;

    public LoginForm(IReadOnlyList<Hostel> hostels)
    {
        Text = "Sign in";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Font;
        Font = UiTheme.BodyFont;
        BackColor = Color.White;
        ClientSize = new Size(420, 340);

        var header = new Panel { Dock = DockStyle.Top, Height = 90, BackColor = UiTheme.NavBackground };
        var titleLabel = new Label
        {
            Dock = DockStyle.Fill,
            Font = UiTheme.HeadingFont,
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleCenter,
            AutoEllipsis = true,
            Text = AppInfo.ProductName,
        };
        header.Controls.Add(titleLabel);

        var fields = FormFields.CreateTable(labelWidth: 100, inputWidth: 240);
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
        fields.Location = new Point(40, 115);

        _messageLabel = FormFields.CreateMessageLabel();
        _messageLabel.Location = new Point(40, 230);
        _messageLabel.Margin = Padding.Empty;

        var signInButton = new Button { Text = "Sign in", TabIndex = 2, Location = new Point(170, 275) };
        UiTheme.StylePrimaryButton(signInButton);
        signInButton.Click += (_, _) => SignIn();

        var cancelButton = new Button { Text = "Exit", TabIndex = 3, Location = new Point(290, 275) };
        UiTheme.StyleSecondaryButton(cancelButton);
        cancelButton.DialogResult = DialogResult.Cancel;

        AcceptButton = signInButton;
        CancelButton = cancelButton;

        Controls.Add(fields);
        Controls.Add(_messageLabel);
        Controls.Add(signInButton);
        Controls.Add(cancelButton);
        Controls.Add(header);

        Shown += (_, _) => _userNameBox.Focus();
    }

    /// <summary>The hostel chosen on the sign in screen, or null when no hostel exists yet.</summary>
    public int? SelectedHostelId => _hostelBox.SelectedValue is int id ? id : null;

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
