using HostelManagement.Models;
using HostelManagement.Services;
using HostelManagement.Utilities;

namespace HostelManagement.Forms;

/// <summary>Add or edit one college. DialogResult.OK means it was saved.</summary>
public sealed class CollegeEditForm : Form
{
    private readonly int _collegeId;
    private readonly TextBox _nameBox;
    private readonly TextBox _addressBox;
    private readonly TextBox _phoneBox;
    private readonly Label _messageLabel;

    public CollegeEditForm(College? college = null)
    {
        _collegeId = college?.CollegeId ?? 0;

        Text = college is null ? "Add College" : "Edit College";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Font;
        Font = UiTheme.BodyFont;
        BackColor = Color.White;
        ClientSize = new Size(600, 260);

        var fields = FormFields.CreateTable(labelWidth: 120, inputWidth: 430);
        _nameBox = FormFields.AddTextBox(fields, "College name", 150, required: true);
        _addressBox = FormFields.AddTextBox(fields, "Address", 255, multiline: true);
        _phoneBox = FormFields.AddTextBox(fields, "Phone", 20);
        fields.Location = new Point(20, 20);

        _nameBox.Text = college?.CollegeName ?? string.Empty;
        _addressBox.Text = college?.Address ?? string.Empty;
        _phoneBox.Text = college?.Phone ?? string.Empty;

        _messageLabel = FormFields.CreateMessageLabel();
        _messageLabel.Location = new Point(20, 170);

        var saveButton = new Button { Text = "Save", TabIndex = 1, Location = new Point(350, 210) };
        UiTheme.StylePrimaryButton(saveButton);
        saveButton.Click += (_, _) => Save();

        var cancelButton = new Button { Text = "Cancel", TabIndex = 2, Location = new Point(470, 210) };
        UiTheme.StyleSecondaryButton(cancelButton);
        cancelButton.DialogResult = DialogResult.Cancel;

        AcceptButton = saveButton;
        CancelButton = cancelButton;

        Controls.Add(fields);
        Controls.Add(_messageLabel);
        Controls.Add(saveButton);
        Controls.Add(cancelButton);
    }

    /// <summary>The saved college, available after DialogResult.OK.</summary>
    public College? SavedCollege { get; private set; }

    private void Save()
    {
        try
        {
            SavedCollege = CollegeService.Save(new College
            {
                CollegeId = _collegeId,
                CollegeName = _nameBox.Text,
                Address = _addressBox.Text,
                Phone = _phoneBox.Text,
            });
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (ValidationException ex)
        {
            FormFields.ShowError(_messageLabel, ex.Message);
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The college could not be saved.");
        }
    }
}
