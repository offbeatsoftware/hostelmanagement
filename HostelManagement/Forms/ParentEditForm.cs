using HostelManagement.Models;
using HostelManagement.Services;
using HostelManagement.Utilities;

namespace HostelManagement.Forms;

/// <summary>Add or edit a parent or guardian. DialogResult.OK means it was saved.</summary>
public sealed class ParentEditForm : Form
{
    private readonly int _parentId;
    private readonly ComboBox _studentBox;
    private readonly TextBox _nameBox;
    private readonly TextBox _relationshipBox;
    private readonly TextBox _mobileBox;
    private readonly TextBox _emailBox;
    private readonly TextBox _addressBox;
    private readonly CheckBox _primaryBox;
    private readonly Label _messageLabel;

    /// <param name="students">Students to choose from when adding; ignored when editing.</param>
    public ParentEditForm(IReadOnlyList<Student> students, Parent? parent = null)
    {
        _parentId = parent?.ParentId ?? 0;

        Text = parent is null ? "Add Parent / Guardian" : "Edit Parent / Guardian";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Font;
        Font = UiTheme.BodyFont;
        BackColor = Color.White;
        ClientSize = new Size(600, 400);

        TableLayoutPanel fields = FormFields.CreateTable(labelWidth: 120, inputWidth: 430);
        _studentBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = UiTheme.BodyFont,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 4, 0, 4),
            DisplayMember = nameof(Student.StudentName),
            ValueMember = nameof(Student.StudentId),
            DataSource = parent is null
                ? students.ToList()
                : [new Student { StudentId = parent.StudentId, StudentName = parent.StudentName }],
            Enabled = parent is null,
        };
        FormFields.AddRow(fields, "Student", _studentBox, required: true);
        _nameBox = FormFields.AddTextBox(fields, "Name", 150, required: true);
        _relationshipBox = FormFields.AddTextBox(fields, "Relationship", 50);
        _mobileBox = FormFields.AddTextBox(fields, "Mobile", 20, required: true);
        _emailBox = FormFields.AddTextBox(fields, "Email", 150, required: true);
        _addressBox = FormFields.AddTextBox(fields, "Address", 255, multiline: true);
        _primaryBox = new CheckBox
        {
            Text = "Primary contact (receives invoices and reminders)",
            AutoSize = true,
            Font = UiTheme.BodyFont,
            Margin = new Padding(0, 6, 0, 4),
        };
        FormFields.AddRow(fields, string.Empty, _primaryBox);
        fields.Location = new Point(20, 20);

        _nameBox.Text = parent?.ParentName ?? string.Empty;
        _relationshipBox.Text = parent?.Relationship ?? string.Empty;
        _mobileBox.Text = parent?.Mobile ?? string.Empty;
        _emailBox.Text = parent?.Email ?? string.Empty;
        _addressBox.Text = parent?.Address ?? string.Empty;
        _primaryBox.Checked = parent?.IsPrimaryContact ?? false;

        _messageLabel = FormFields.CreateMessageLabel();
        _messageLabel.Location = new Point(20, 310);
        _messageLabel.MaximumSize = new Size(560, 0);

        var saveButton = new Button { Text = "Save", Location = new Point(350, 350) };
        UiTheme.StylePrimaryButton(saveButton);
        saveButton.Click += (_, _) => Save();

        var cancelButton = new Button { Text = "Cancel", Location = new Point(470, 350) };
        UiTheme.StyleSecondaryButton(cancelButton);
        cancelButton.DialogResult = DialogResult.Cancel;

        AcceptButton = saveButton;
        CancelButton = cancelButton;

        Controls.Add(fields);
        Controls.Add(_messageLabel);
        Controls.Add(saveButton);
        Controls.Add(cancelButton);
    }

    /// <summary>The saved parent, available after DialogResult.OK.</summary>
    public Parent? SavedParent { get; private set; }

    private void Save()
    {
        try
        {
            SavedParent = ParentService.Save(new Parent
            {
                ParentId = _parentId,
                StudentId = _studentBox.SelectedValue is int studentId ? studentId : 0,
                ParentName = _nameBox.Text,
                Relationship = _relationshipBox.Text,
                Mobile = _mobileBox.Text,
                Email = _emailBox.Text,
                Address = _addressBox.Text,
                IsPrimaryContact = _primaryBox.Checked,
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
            ErrorHandler.Handle(ex, "The parent or guardian could not be saved.");
        }
    }
}
