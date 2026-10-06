using System.Diagnostics;
using HostelManagement.Models;
using HostelManagement.Services;
using HostelManagement.Utilities;

namespace HostelManagement.Forms;

/// <summary>
/// Add, view or edit a student with the primary parent, photo and Aadhaar card.
/// DialogResult.OK means it was saved.
/// </summary>
public sealed class StudentEditForm : Form
{
    private readonly int _studentId;
    private readonly int _parentId;
    private readonly string _currentPhotoPath;
    private readonly string _currentAadhaarCardPath;

    // Student tab
    private readonly TextBox _nameBox;
    private readonly ComboBox _genderBox;
    private readonly DateTimePicker _birthPicker;
    private readonly TextBox _mobileBox;
    private readonly TextBox _emailBox;
    private readonly TextBox _addressBox;
    private readonly TextBox _aadhaarBox;

    // College & stay tab
    private readonly ComboBox _collegeBox;
    private readonly TextBox _courseBox;
    private readonly TextBox _classBox;
    private readonly DateTimePicker _admissionPicker;
    private readonly ComboBox _statusBox;
    private readonly TextBox _remarksBox;

    // Parent tab
    private readonly TextBox _parentNameBox;
    private readonly TextBox _relationshipBox;
    private readonly TextBox _parentMobileBox;
    private readonly TextBox _parentEmailBox;
    private readonly TextBox _parentAddressBox;

    // Photo and Aadhaar card tab
    private readonly PictureBox _photoBox;
    private readonly Label _photoStatus;
    private readonly Label _aadhaarCardStatus;
    private readonly Button _viewAadhaarCardButton;
    private string? _newPhotoFile;
    private bool _removePhoto;
    private string? _newAadhaarCardFile;
    private bool _removeAadhaarCard;

    // Services tab
    private readonly CheckedListBox _servicesList;

    private readonly TabControl _tabs;
    private readonly Label _messageLabel;

    /// <param name="extraServices">Extra services the student can use (active ones plus any already in use).</param>
    /// <param name="includedServices">Services included in the rent, shown for information.</param>
    /// <param name="currentServiceIds">The extra services the student uses now.</param>
    public StudentEditForm(IReadOnlyList<College> colleges, Student? student = null, Parent? primaryParent = null,
        IReadOnlyList<ServiceItem>? extraServices = null, IReadOnlyList<ServiceItem>? includedServices = null,
        IReadOnlyCollection<int>? currentServiceIds = null)
    {
        _studentId = student?.StudentId ?? 0;
        _parentId = primaryParent?.ParentId ?? 0;
        _currentPhotoPath = student?.PhotoPath ?? string.Empty;
        _currentAadhaarCardPath = student?.AadhaarCardPath ?? string.Empty;

        Text = student is null ? "Add Student" : $"Student: {student.StudentName}";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Font;
        Font = UiTheme.BodyFont;
        BackColor = Color.White;
        ClientSize = new Size(640, 500);

        _tabs = new TabControl { Location = new Point(12, 12), Size = new Size(616, 390), Font = UiTheme.BodyFont };

        // ---- Student ----
        TableLayoutPanel studentFields = FormFields.CreateTable(labelWidth: 140, inputWidth: 420);
        _nameBox = FormFields.AddTextBox(studentFields, "Student name", 150, required: true);
        _genderBox = AddCombo(studentFields, "Gender", ["", .. Student.Genders]);
        _birthPicker = AddDatePicker(studentFields, "Date of birth", optional: true);
        _mobileBox = FormFields.AddTextBox(studentFields, "Mobile", 20, required: true);
        _emailBox = FormFields.AddTextBox(studentFields, "Email", 150);
        _addressBox = FormFields.AddTextBox(studentFields, "Address", 255, multiline: true);
        _aadhaarBox = FormFields.AddTextBox(studentFields, "Aadhaar number", 14);
        AddTab("Student", studentFields);

        // ---- College & stay ----
        TableLayoutPanel stayFields = FormFields.CreateTable(labelWidth: 140, inputWidth: 420);
        _collegeBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = UiTheme.BodyFont,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 4, 0, 4),
            DisplayMember = nameof(College.CollegeName),
            ValueMember = nameof(College.CollegeId),
            DataSource = colleges.ToList(),
        };
        FormFields.AddRow(stayFields, "College", _collegeBox, required: true);
        _courseBox = FormFields.AddTextBox(stayFields, "Course", 100);
        _classBox = FormFields.AddTextBox(stayFields, "Class", 50);
        _admissionPicker = AddDatePicker(stayFields, "Admission date", optional: false, required: true);
        _statusBox = AddCombo(stayFields, "Status", StudentStatus.All, required: true);
        _remarksBox = FormFields.AddTextBox(stayFields, "Remarks", 255, multiline: true);
        AddTab("College & Stay", stayFields);

        // ---- Parent / guardian ----
        TableLayoutPanel parentFields = FormFields.CreateTable(labelWidth: 140, inputWidth: 420);
        _parentNameBox = FormFields.AddTextBox(parentFields, "Name", 150, required: true);
        _relationshipBox = FormFields.AddTextBox(parentFields, "Relationship", 50);
        _parentMobileBox = FormFields.AddTextBox(parentFields, "Mobile", 20, required: true);
        _parentEmailBox = FormFields.AddTextBox(parentFields, "Email", 150, required: true);
        _parentAddressBox = FormFields.AddTextBox(parentFields, "Address", 255, multiline: true);
        var parentNote = new Label
        {
            AutoSize = true,
            Font = UiTheme.BodyFont,
            ForeColor = UiTheme.TextMuted,
            Text = "Primary contact: receives invoices and due reminders.\nMore guardians can be added on the Parents / Guardians screen.",
            Margin = new Padding(0, 8, 0, 0),
        };
        FormFields.AddRow(parentFields, string.Empty, parentNote);
        AddTab("Parent / Guardian", parentFields);

        // ---- Photo and Aadhaar card ----
        var filesPanel = new Panel { Dock = DockStyle.Fill };
        _photoBox = new PictureBox
        {
            Location = new Point(10, 10),
            Size = new Size(150, 180),
            BorderStyle = BorderStyle.FixedSingle,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = UiTheme.ContentBackground,
        };
        var choosePhotoButton = new Button { Text = "Choose Photo", Location = new Point(180, 10) };
        UiTheme.StyleSecondaryButton(choosePhotoButton);
        choosePhotoButton.Width = 140;
        choosePhotoButton.Click += (_, _) => ChoosePhoto();
        var removePhotoButton = new Button { Text = "Remove Photo", Location = new Point(180, 52) };
        UiTheme.StyleSecondaryButton(removePhotoButton);
        removePhotoButton.Width = 140;
        removePhotoButton.Click += (_, _) => RemovePhoto();
        _photoStatus = new Label { AutoSize = true, Location = new Point(180, 100), ForeColor = UiTheme.TextMuted };

        var aadhaarHeading = new Label
        {
            AutoSize = true,
            Location = new Point(10, 215),
            Font = UiTheme.BodyBoldFont,
            Text = "Aadhaar card (scan or photo, PDF/JPG/PNG, max 5 MB)",
        };
        _aadhaarCardStatus = new Label { AutoSize = true, Location = new Point(10, 245), ForeColor = UiTheme.TextMuted };
        var chooseCardButton = new Button { Text = "Choose File", Location = new Point(10, 275) };
        UiTheme.StyleSecondaryButton(chooseCardButton);
        chooseCardButton.Click += (_, _) => ChooseAadhaarCard();
        _viewAadhaarCardButton = new Button { Text = "View", Location = new Point(130, 275) };
        UiTheme.StyleSecondaryButton(_viewAadhaarCardButton);
        _viewAadhaarCardButton.Click += (_, _) => ViewAadhaarCard();
        var removeCardButton = new Button { Text = "Remove", Location = new Point(250, 275) };
        UiTheme.StyleSecondaryButton(removeCardButton);
        removeCardButton.Click += (_, _) => RemoveAadhaarCard();

        filesPanel.Controls.AddRange(
        [
            _photoBox, choosePhotoButton, removePhotoButton, _photoStatus,
            aadhaarHeading, _aadhaarCardStatus, chooseCardButton, _viewAadhaarCardButton, removeCardButton,
        ]);
        AddTab("Photo & Aadhaar", filesPanel);

        // ---- Services ----
        var servicesPanel = new Panel { Dock = DockStyle.Fill };
        var includedLabel = new Label
        {
            AutoSize = true,
            Location = new Point(10, 10),
            MaximumSize = new Size(560, 0),
            ForeColor = UiTheme.TextMuted,
            Text = includedServices is { Count: > 0 }
                ? "Included in the room rent: " + string.Join(", ", includedServices.Select(s => s.ServiceName)) + "."
                : "No services are included in the room rent.",
        };
        var extraHeading = new Label
        {
            AutoSize = true,
            Location = new Point(10, 45),
            Font = UiTheme.BodyBoldFont,
            Text = "Extra services used by this student (charged per month)",
        };
        _servicesList = new CheckedListBox
        {
            Location = new Point(10, 72),
            Size = new Size(360, 150),
            CheckOnClick = true,
            Font = UiTheme.BodyFont,
            DisplayMember = nameof(ServiceItem.ServiceName),
        };
        foreach (ServiceItem service in extraServices ?? [])
        {
            string text = $"{service.ServiceName}  ({Money.Format(service.MonthlyRate)} per month)";
            _servicesList.Items.Add(new ServiceChoice(service.ServiceId, text),
                currentServiceIds?.Contains(service.ServiceId) == true);
        }
        _servicesList.DisplayMember = nameof(ServiceChoice.Text);
        var servicesNote = new Label
        {
            AutoSize = true,
            Location = new Point(10, 232),
            MaximumSize = new Size(560, 0),
            ForeColor = UiTheme.TextMuted,
            Text = extraServices is { Count: > 0 }
                ? "A ticked service is charged from today (from the admission date for a new student); unticking stops it today."
                : "This hostel has no active extra services. Add them on the Services screen.",
        };
        servicesPanel.Controls.AddRange([includedLabel, extraHeading, _servicesList, servicesNote]);
        AddTab("Services", servicesPanel);

        // ---- Buttons ----
        _messageLabel = FormFields.CreateMessageLabel();
        _messageLabel.Location = new Point(14, 410);
        _messageLabel.MaximumSize = new Size(610, 0);

        var saveButton = new Button { Text = "Save", Location = new Point(390, 450) };
        UiTheme.StylePrimaryButton(saveButton);
        saveButton.Click += (_, _) => Save();

        var cancelButton = new Button { Text = "Cancel", Location = new Point(510, 450) };
        UiTheme.StyleSecondaryButton(cancelButton);
        cancelButton.DialogResult = DialogResult.Cancel;

        AcceptButton = saveButton;
        CancelButton = cancelButton;

        Controls.Add(_tabs);
        Controls.Add(_messageLabel);
        Controls.Add(saveButton);
        Controls.Add(cancelButton);

        ShowValues(student, primaryParent);
        Load += (_, _) =>
        {
            // The college list is filled by data binding once the form exists.
            if (student is not null)
            {
                _collegeBox.SelectedValue = student.CollegeId;
            }
            ShowFiles();
        };
        FormClosed += (_, _) => _photoBox.Image?.Dispose();
    }

    /// <summary>The saved student, available after DialogResult.OK.</summary>
    public Student? SavedStudent { get; private set; }

    private void AddTab(string title, Control content)
    {
        var page = new TabPage(title) { BackColor = Color.White, Padding = new Padding(12), AutoScroll = true };
        content.Location = new Point(12, 12);
        page.Controls.Add(content);
        _tabs.TabPages.Add(page);
    }

    private static ComboBox AddCombo(TableLayoutPanel table, string caption, IEnumerable<string> items, bool required = false)
    {
        var combo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = UiTheme.BodyFont,
            Width = 200,
            Margin = new Padding(0, 4, 0, 4),
        };
        combo.Items.AddRange(items.Cast<object>().ToArray());
        FormFields.AddRow(table, caption, combo, required);
        return combo;
    }

    private static DateTimePicker AddDatePicker(TableLayoutPanel table, string caption, bool optional, bool required = false)
    {
        var picker = new DateTimePicker
        {
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "dd MMM yyyy",
            Font = UiTheme.BodyFont,
            Width = 200,
            ShowCheckBox = optional,
            Margin = new Padding(0, 4, 0, 4),
        };
        FormFields.AddRow(table, caption, picker, required);
        return picker;
    }

    private void ShowValues(Student? student, Parent? parent)
    {
        _nameBox.Text = student?.StudentName ?? string.Empty;
        _genderBox.SelectedItem = student?.Gender ?? string.Empty;
        _birthPicker.Value = student?.DateOfBirth ?? DateTime.Today.AddYears(-18);
        _birthPicker.Checked = student?.DateOfBirth is not null;
        _mobileBox.Text = student?.Mobile ?? string.Empty;
        _emailBox.Text = student?.Email ?? string.Empty;
        _addressBox.Text = student?.Address ?? string.Empty;
        _aadhaarBox.Text = student?.AadhaarNumber ?? string.Empty;

        _courseBox.Text = student?.Course ?? string.Empty;
        _classBox.Text = student?.ClassName ?? string.Empty;
        _admissionPicker.Value = student?.AdmissionDate ?? DateTime.Today;
        _statusBox.SelectedItem = student?.Status ?? StudentStatus.Active;
        _remarksBox.Text = student?.Remarks ?? string.Empty;

        _parentNameBox.Text = parent?.ParentName ?? string.Empty;
        _relationshipBox.Text = parent?.Relationship ?? string.Empty;
        _parentMobileBox.Text = parent?.Mobile ?? string.Empty;
        _parentEmailBox.Text = parent?.Email ?? string.Empty;
        _parentAddressBox.Text = parent?.Address ?? string.Empty;
    }

    // ---- Photo and Aadhaar card ----

    private void ShowFiles()
    {
        _photoBox.Image?.Dispose();
        _photoBox.Image = null;

        if (_newPhotoFile is not null)
        {
            _photoBox.Image = LoadPreview(_newPhotoFile);
            _photoStatus.Text = "New photo (saved when you click Save)";
        }
        else if (_removePhoto || _currentPhotoPath.Length == 0)
        {
            _photoStatus.Text = "No photo";
        }
        else
        {
            _photoBox.Image = StudentFileService.LoadPhoto(_currentPhotoPath);
            _photoStatus.Text = _photoBox.Image is null ? "Photo file is missing" : string.Empty;
        }

        if (_newAadhaarCardFile is not null)
        {
            _aadhaarCardStatus.Text = $"New file: {Path.GetFileName(_newAadhaarCardFile)} (saved when you click Save)";
        }
        else if (_removeAadhaarCard || _currentAadhaarCardPath.Length == 0)
        {
            _aadhaarCardStatus.Text = "No Aadhaar card file";
        }
        else
        {
            _aadhaarCardStatus.Text = StudentFileService.Exists(_currentAadhaarCardPath)
                ? "Aadhaar card file saved"
                : "Aadhaar card file is missing";
        }
        _viewAadhaarCardButton.Enabled = CurrentAadhaarCardFile() is not null;
    }

    private static Image? LoadPreview(string file)
    {
        try
        {
            using var stream = new MemoryStream(File.ReadAllBytes(file));
            using var image = Image.FromStream(stream);
            return new Bitmap(image);
        }
        catch (Exception ex) when (ex is ArgumentException or OutOfMemoryException or IOException)
        {
            return null;
        }
    }

    private string? ChooseFile(string title, string filter)
    {
        using var dialog = new OpenFileDialog { Title = title, Filter = filter, CheckFileExists = true };
        return dialog.ShowDialog(this) == DialogResult.OK ? dialog.FileName : null;
    }

    private void ChoosePhoto()
    {
        if (ChooseFile("Choose student photo", StudentFileService.PhotoFileFilter) is not string file)
        {
            return;
        }

        try
        {
            StudentFileService.ValidatePhoto(file);
            _newPhotoFile = file;
            _removePhoto = false;
            ShowFiles();
        }
        catch (ValidationException ex)
        {
            FormFields.ShowError(_messageLabel, ex.Message);
        }
    }

    private void RemovePhoto()
    {
        _newPhotoFile = null;
        _removePhoto = _currentPhotoPath.Length > 0;
        ShowFiles();
    }

    private void ChooseAadhaarCard()
    {
        if (ChooseFile("Choose Aadhaar card file", StudentFileService.AadhaarCardFileFilter) is not string file)
        {
            return;
        }

        try
        {
            StudentFileService.ValidateAadhaarCard(file);
            _newAadhaarCardFile = file;
            _removeAadhaarCard = false;
            ShowFiles();
        }
        catch (ValidationException ex)
        {
            FormFields.ShowError(_messageLabel, ex.Message);
        }
    }

    private void RemoveAadhaarCard()
    {
        _newAadhaarCardFile = null;
        _removeAadhaarCard = _currentAadhaarCardPath.Length > 0;
        ShowFiles();
    }

    private string? CurrentAadhaarCardFile()
    {
        if (_newAadhaarCardFile is not null)
        {
            return _newAadhaarCardFile;
        }
        return !_removeAadhaarCard && StudentFileService.Exists(_currentAadhaarCardPath)
            ? StudentFileService.FullPath(_currentAadhaarCardPath)
            : null;
    }

    private void ViewAadhaarCard()
    {
        if (CurrentAadhaarCardFile() is not string file)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(file) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The Aadhaar card file could not be opened. Check that a PDF or picture viewer is installed.");
        }
    }

    // ---- Save ----

    private void Save()
    {
        try
        {
            SavedStudent = StudentService.Save(
                new Student
                {
                    StudentId = _studentId,
                    StudentName = _nameBox.Text,
                    Gender = _genderBox.SelectedItem as string ?? string.Empty,
                    DateOfBirth = _birthPicker.Checked ? _birthPicker.Value.Date : null,
                    Mobile = _mobileBox.Text,
                    Email = _emailBox.Text,
                    Address = _addressBox.Text,
                    AadhaarNumber = _aadhaarBox.Text,
                    CollegeId = _collegeBox.SelectedValue is int collegeId ? collegeId : 0,
                    Course = _courseBox.Text,
                    ClassName = _classBox.Text,
                    AdmissionDate = _admissionPicker.Value.Date,
                    Status = _statusBox.SelectedItem as string ?? string.Empty,
                    Remarks = _remarksBox.Text,
                },
                new Parent
                {
                    ParentId = _parentId,
                    ParentName = _parentNameBox.Text,
                    Relationship = _relationshipBox.Text,
                    Mobile = _parentMobileBox.Text,
                    Email = _parentEmailBox.Text,
                    Address = _parentAddressBox.Text,
                },
                new StudentFileChanges(_newPhotoFile, _removePhoto, _newAadhaarCardFile, _removeAadhaarCard),
                _servicesList.CheckedItems.Cast<ServiceChoice>().Select(c => c.ServiceId).ToList());

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (ValidationException ex)
        {
            FormFields.ShowError(_messageLabel, ex.Message);
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The student could not be saved.");
        }
    }

    /// <summary>An item in the extra services list.</summary>
    private sealed record ServiceChoice(int ServiceId, string Text)
    {
        public override string ToString() => Text;
    }
}
