using HostelManagement.Services;
using HostelManagement.Utilities;

namespace HostelManagement.Forms;

/// <summary>Admin sign in shown before the main window. DialogResult.OK means signed in.</summary>
public sealed class LoginForm : Form
{
    private readonly TextBox _userNameBox;
    private readonly TextBox _passwordBox;
    private readonly Label _messageLabel;

    public LoginForm(string hostelName)
    {
        Text = "Sign in";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Font;
        Font = UiTheme.BodyFont;
        BackColor = Color.White;
        ClientSize = new Size(420, 330);

        var header = new Panel { Dock = DockStyle.Top, Height = 90, BackColor = UiTheme.NavBackground };
        var titleLabel = new Label
        {
            Dock = DockStyle.Fill,
            Font = UiTheme.HeadingFont,
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleCenter,
            AutoEllipsis = true,
            Text = string.IsNullOrWhiteSpace(hostelName) ? AppInfo.ProductName : hostelName,
        };
        header.Controls.Add(titleLabel);

        var fields = FormFields.CreateTable(labelWidth: 100, inputWidth: 240);
        _userNameBox = FormFields.AddTextBox(fields, "User name", 50);
        _passwordBox = FormFields.AddTextBox(fields, "Password", 50);
        _passwordBox.UseSystemPasswordChar = true;
        fields.Location = new Point(40, 115);

        _messageLabel = FormFields.CreateMessageLabel();
        _messageLabel.Location = new Point(40, 195);
        _messageLabel.Margin = Padding.Empty;

        var signInButton = new Button { Text = "Sign in", TabIndex = 2, Location = new Point(170, 250) };
        UiTheme.StylePrimaryButton(signInButton);
        signInButton.Click += (_, _) => SignIn();

        var cancelButton = new Button { Text = "Exit", TabIndex = 3, Location = new Point(290, 250) };
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
