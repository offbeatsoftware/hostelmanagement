using HostelManagement.Models;
using HostelManagement.Services;
using HostelManagement.Utilities;

namespace HostelManagement.Forms.Views;

/// <summary>Admin Account: the admin's email (copied on every email to parents) and phone, and changing the password.</summary>
public sealed class AdminAccountView : UserControl
{
    private readonly TextBox _userNameBox;
    private readonly TextBox _emailBox;
    private readonly TextBox _phoneBox;
    private readonly Label _contactMessage;
    private readonly TextBox _currentPasswordBox;
    private readonly TextBox _newPasswordBox;
    private readonly TextBox _confirmPasswordBox;
    private readonly Label _passwordMessage;

    public AdminAccountView()
    {
        Dock = DockStyle.Fill;
        BackColor = UiTheme.ContentBackground;

        // ---- Contact details ----
        TableLayoutPanel contact = FormFields.CreateTable(labelWidth: 150, inputWidth: 340);
        _userNameBox = FormFields.AddTextBox(contact, "User name", 50);
        _userNameBox.ReadOnly = true;
        _emailBox = FormFields.AddTextBox(contact, "Email", 150);
        _phoneBox = FormFields.AddTextBox(contact, "Phone", 20);
        var contactNote = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(500, 0),
            ForeColor = UiTheme.TextMuted,
            Margin = new Padding(0, 4, 0, 8),
            Text = "A copy of every email sent to parents (invoices, receipts and reminders) goes to this email address. " +
                   "Email and phone can also be used in the email texts as {AdminEmail} and {AdminPhone}.",
        };
        FormFields.AddRow(contact, string.Empty, contactNote);

        var saveButton = new Button { Text = "Save" };
        UiTheme.StylePrimaryButton(saveButton);
        saveButton.Click += (_, _) => SaveContact();
        _contactMessage = FormFields.CreateMessageLabel();
        FlowLayoutPanel contactButtons = FormFields.CreateButtonRow(saveButton, _contactMessage);
        FormFields.AddRow(contact, string.Empty, contactButtons);

        Panel contactCard = FormFields.CreateCard("Admin details", contact);
        contactCard.Dock = DockStyle.Top;
        contactCard.Height = 270;

        // ---- Password ----
        TableLayoutPanel password = FormFields.CreateTable(labelWidth: 150, inputWidth: 260);
        _currentPasswordBox = PasswordBox(password, "Current password");
        _newPasswordBox = PasswordBox(password, "New password");
        _confirmPasswordBox = PasswordBox(password, "Confirm new password");
        var passwordNote = new Label
        {
            AutoSize = true,
            ForeColor = UiTheme.TextMuted,
            Margin = new Padding(0, 4, 0, 8),
            Text = $"At least {AuthService.MinimumPasswordLength} characters. Passwords are case sensitive.",
        };
        FormFields.AddRow(password, string.Empty, passwordNote);

        var changeButton = new Button { Text = "Change Password" };
        UiTheme.StylePrimaryButton(changeButton);
        changeButton.Width = 160;
        changeButton.Click += (_, _) => ChangePassword();
        _passwordMessage = FormFields.CreateMessageLabel();
        FlowLayoutPanel passwordButtons = FormFields.CreateButtonRow(changeButton, _passwordMessage);
        FormFields.AddRow(password, string.Empty, passwordButtons);

        Panel passwordCard = FormFields.CreateCard("Change password", password);
        passwordCard.Dock = DockStyle.Top;
        passwordCard.Height = 250;

        var gap = new Panel { Dock = DockStyle.Top, Height = 10 };
        Controls.Add(passwordCard);
        Controls.Add(gap);
        Controls.Add(contactCard);

        Load += (_, _) => LoadAdmin();
    }

    private static TextBox PasswordBox(TableLayoutPanel table, string caption)
    {
        TextBox box = FormFields.AddTextBox(table, caption, 100, required: true);
        box.UseSystemPasswordChar = true;
        return box;
    }

    private void LoadAdmin()
    {
        try
        {
            ShowAdmin(AuthService.GetCurrentAdmin());
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The admin details could not be loaded.");
        }
    }

    private void ShowAdmin(AdminUser admin)
    {
        _userNameBox.Text = admin.UserName;
        _emailBox.Text = admin.Email;
        _phoneBox.Text = admin.Phone;
    }

    private void SaveContact()
    {
        _contactMessage.Text = string.Empty;
        try
        {
            ShowAdmin(AuthService.SaveContact(_emailBox.Text, _phoneBox.Text));
            FormFields.ShowSuccess(_contactMessage, "Saved.");
        }
        catch (ValidationException ex)
        {
            FormFields.ShowError(_contactMessage, ex.Message);
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The admin details could not be saved.");
        }
    }

    private void ChangePassword()
    {
        _passwordMessage.Text = string.Empty;
        try
        {
            AuthService.ChangePassword(_currentPasswordBox.Text, _newPasswordBox.Text, _confirmPasswordBox.Text);
            _currentPasswordBox.Clear();
            _newPasswordBox.Clear();
            _confirmPasswordBox.Clear();
            FormFields.ShowSuccess(_passwordMessage, "Password changed. Use the new password next time you sign in.");
        }
        catch (ValidationException ex)
        {
            FormFields.ShowError(_passwordMessage, ex.Message);
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The password could not be changed.");
        }
    }
}
