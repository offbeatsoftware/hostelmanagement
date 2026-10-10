using HostelManagement.Models;
using HostelManagement.Services;
using HostelManagement.Utilities;

namespace HostelManagement.Forms;

/// <summary>Add or edit one hostel. DialogResult.OK means it was saved.</summary>
public sealed class HostelEditForm : Form
{
    private readonly int _hostelId;
    private readonly TextBox _nameBox;
    private readonly TextBox _addressBox;
    private readonly TextBox _phoneBox;
    private readonly TextBox _emailBox;
    private readonly Label _messageLabel;

    public HostelEditForm(Hostel? hostel = null)
    {
        _hostelId = hostel?.HostelId ?? 0;

        Text = hostel is null ? "Add Hostel" : "Edit Hostel";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Font;
        Font = UiTheme.BodyFont;
        BackColor = Color.White;
        ClientSize = new Size(600, 320);

        var fields = FormFields.CreateTable(labelWidth: 120, inputWidth: 430);
        _nameBox = FormFields.AddTextBox(fields, "Hostel name", 150, required: true);
        _addressBox = FormFields.AddTextBox(fields, "Address", 255, multiline: true);
        _phoneBox = FormFields.AddTextBox(fields, "Phone", 20);
        _emailBox = FormFields.AddTextBox(fields, "Email", 150);
        fields.Location = new Point(20, 20);

        _nameBox.Text = hostel?.HostelName ?? string.Empty;
        _addressBox.Text = hostel?.Address ?? string.Empty;
        _phoneBox.Text = hostel?.Phone ?? string.Empty;
        _emailBox.Text = hostel?.Email ?? string.Empty;

        _messageLabel = FormFields.CreateMessageLabel();
        _messageLabel.Location = new Point(20, 230);
        _messageLabel.MaximumSize = new Size(560, 0);

        var saveButton = new Button { Text = "Save", TabIndex = 1, Location = new Point(350, 270) };
        UiTheme.StylePrimaryButton(saveButton);
        saveButton.Click += (_, _) => Save();

        var cancelButton = new Button { Text = "Cancel", TabIndex = 2, Location = new Point(470, 270) };
        UiTheme.StyleSecondaryButton(cancelButton);
        cancelButton.DialogResult = DialogResult.Cancel;

        AcceptButton = saveButton;
        CancelButton = cancelButton;

        Controls.Add(fields);
        Controls.Add(_messageLabel);
        Controls.Add(saveButton);
        Controls.Add(cancelButton);
    }

    /// <summary>The saved hostel, available after DialogResult.OK.</summary>
    public Hostel? SavedHostel { get; private set; }

    private void Save()
    {
        try
        {
            SavedHostel = HostelService.Save(new Hostel
            {
                HostelId = _hostelId,
                HostelName = _nameBox.Text,
                Address = _addressBox.Text,
                Phone = _phoneBox.Text,
                Email = _emailBox.Text,
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
            ErrorHandler.Handle(ex, "The hostel could not be saved.");
        }
    }
}
