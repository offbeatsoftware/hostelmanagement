using HostelManagement.Models;
using HostelManagement.Services;
using HostelManagement.Utilities;

namespace HostelManagement.Forms.Views;

/// <summary>
/// Students of the selected hostel with search, filters, add, edit and delete, the agreement PDF and I-cards
/// (select one or more students for I-cards). Students who have checked out are on the separate "Inactive students"
/// tab, where a refund can still be given, the student made active again or deleted permanently with all records.
/// </summary>
public sealed class StudentsView : UserControl
{
    private const string AllColleges = "All colleges";
    private const string AllCourses = "All courses";
    private const string AllClasses = "All classes";

    private readonly int _hostelId;
    private readonly TextBox _searchBox;
    private readonly ComboBox _collegeFilter;
    private readonly ComboBox _courseFilter;
    private readonly ComboBox _classFilter;
    private readonly RadioButton _activeTab;
    private readonly RadioButton _inactiveTab;
    private readonly DataGridView _grid;
    private readonly Button _editButton;
    private readonly Button _deleteButton;
    private readonly Button _agreementButton;
    private readonly Button _signButton;
    private readonly Button _idCardButton;
    private readonly Button _transportCardButton;
    private readonly Button _refundButton;
    private readonly Button _makeActiveButton;
    private readonly Button _deletePermanentlyButton;
    private readonly Button _addButton;
    private readonly Label _countLabel;

    private List<Student> _students = [];
    private List<College> _colleges = [];

    public StudentsView(Hostel hostel)
    {
        _hostelId = hostel.HostelId;
        Dock = DockStyle.Fill;
        BackColor = UiTheme.ContentBackground;

        _searchBox = new TextBox
        {
            Font = UiTheme.BodyFont,
            Width = 260,
            PlaceholderText = "Search name or mobile (student or father)",
            Margin = new Padding(0, 4, 8, 0),
        };
        _searchBox.TextChanged += (_, _) => ShowStudents();

        _collegeFilter = CreateFilter(180);
        _courseFilter = CreateFilter(150);
        _classFilter = CreateFilter(120);

        // Two large tab buttons (a TabControl strip can come out only a few pixels high on some screens).
        _activeTab = CreateTab("Active students");
        _inactiveTab = CreateTab("Inactive students");
        _activeTab.Checked = true;
        StyleTab(_activeTab);
        StyleTab(_inactiveTab);
        FlowLayoutPanel tabRow = FormFields.CreateButtonRow(_activeTab, _inactiveTab);
        tabRow.Dock = DockStyle.Top;

        FlowLayoutPanel filterRow = FormFields.CreateButtonRow(
            _searchBox, _collegeFilter, _courseFilter, _classFilter);
        filterRow.Dock = DockStyle.Top;

        _addButton = new Button { Text = "Add" };
        UiTheme.StylePrimaryButton(_addButton);
        _addButton.Click += (_, _) => AddStudent();

        _editButton = new Button { Text = "View / Edit" };
        UiTheme.StyleSecondaryButton(_editButton);
        _editButton.Width = 120;
        _editButton.Click += (_, _) => EditSelectedStudent();

        _deleteButton = new Button { Text = "Delete" };
        UiTheme.StyleDangerButton(_deleteButton);
        _deleteButton.Click += (_, _) => DeleteSelectedStudent();

        _agreementButton = new Button { Text = "Agreement PDF" };
        UiTheme.StyleSecondaryButton(_agreementButton);
        _agreementButton.Width = 140;
        _agreementButton.Click += (_, _) =>
        {
            if (SelectedStudent is Student student)
            {
                AgreementActions.Download(this, student.StudentId);
            }
        };

        _signButton = new Button { Text = "Sign Agreement" };
        UiTheme.StyleSecondaryButton(_signButton);
        _signButton.Width = 140;
        _signButton.Click += (_, _) =>
        {
            if (SelectedStudent is Student student)
            {
                AgreementActions.Sign(this, student);
            }
        };

        var agreementTextButton = new Button { Text = "Agreement Text" };
        UiTheme.StyleSecondaryButton(agreementTextButton);
        agreementTextButton.Width = 130;
        agreementTextButton.Click += (_, _) =>
        {
            using var dialog = new AgreementTextForm();
            dialog.ShowDialog(this);
        };

        _idCardButton = new Button { Text = "I-Card" };
        UiTheme.StyleSecondaryButton(_idCardButton);
        _idCardButton.Width = 90;
        _idCardButton.Click += (_, _) => IdCardActions.Download(this, SelectedStudentIds(), transport: false);

        _transportCardButton = new Button { Text = "Transport I-Card" };
        UiTheme.StyleSecondaryButton(_transportCardButton);
        _transportCardButton.Width = 140;
        _transportCardButton.Click += (_, _) => IdCardActions.Download(this, SelectedStudentIds(), transport: true);

        _refundButton = new Button { Text = "Refund" };
        UiTheme.StyleSecondaryButton(_refundButton);
        _refundButton.Click += async (_, _) =>
        {
            if (SelectedStudent is Student student && await RefundActions.RefundAsync(this, student.StudentId) is not null)
            {
                LoadStudents(student.StudentId);
            }
        };

        _makeActiveButton = new Button { Text = "Make Active" };
        UiTheme.StyleSecondaryButton(_makeActiveButton);
        _makeActiveButton.Width = Math.Max(120, TextRenderer.MeasureText(_makeActiveButton.Text, _makeActiveButton.Font).Width + 32);
        _makeActiveButton.Click += (_, _) => MakeSelectedActive();

        _deletePermanentlyButton = new Button { Text = "Delete Permanently" };
        UiTheme.StyleDangerButton(_deletePermanentlyButton);
        _deletePermanentlyButton.Width = Math.Max(160, TextRenderer.MeasureText(_deletePermanentlyButton.Text, _deletePermanentlyButton.Font).Width + 32);
        _deletePermanentlyButton.Click += (_, _) => DeleteSelectedPermanently();

        _countLabel = FormFields.CreateMessageLabel();
        _countLabel.ForeColor = UiTheme.TextMuted;

        FlowLayoutPanel buttonRow = FormFields.CreateButtonRow(
            _addButton, _editButton, _deleteButton, _refundButton, _makeActiveButton, _deletePermanentlyButton, _signButton,
            _agreementButton, agreementTextButton, _idCardButton, _transportCardButton, _countLabel);
        buttonRow.Dock = DockStyle.Top;

        _grid = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false };
        UiTheme.StyleGrid(_grid);
        _grid.MultiSelect = true;
        FormFields.AddGridColumn(_grid, nameof(Student.StudentName), "Student", 16);
        FormFields.AddGridColumn(_grid, nameof(Student.Mobile), "Mobile", 10);
        FormFields.AddGridColumn(_grid, nameof(Student.RoomNumber), "Room", 6);
        FormFields.AddGridColumn(_grid, nameof(Student.CollegeName), "College", 14);
        FormFields.AddGridColumn(_grid, nameof(Student.Course), "Course", 10);
        FormFields.AddGridColumn(_grid, nameof(Student.ClassName), "Class", 7);
        FormFields.AddGridColumn(_grid, nameof(Student.FatherName), "Father", 12);
        FormFields.AddGridColumn(_grid, nameof(Student.FatherMobile), "Father mobile", 10);
        FormFields.AddGridColumn(_grid, nameof(Student.AadhaarMasked), "Aadhaar", 10);
        FormFields.AddGridColumn(_grid, nameof(Student.AdmissionDate), "Admission", 8, format: "dd MMM yyyy");
        FormFields.AddGridColumn(_grid, nameof(Student.Status), "Status", 6);
        _grid.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0)
            {
                EditSelectedStudent();
            }
        };
        _grid.SelectionChanged += (_, _) => UpdateButtons();

        var body = new Panel { Dock = DockStyle.Fill };
        body.Controls.Add(_grid);
        body.Controls.Add(buttonRow);
        body.Controls.Add(filterRow);
        body.Controls.Add(tabRow);
        Panel card = FormFields.CreateCard($"Students of {hostel.HostelName}", body);
        card.Dock = DockStyle.Fill;
        Controls.Add(card);

        // Connected last: setting the default filter above must not refresh a grid that does not exist yet.
        foreach (ComboBox filter in new[] { _collegeFilter, _courseFilter, _classFilter })
        {
            filter.SelectedIndexChanged += (_, _) => ShowStudents();
        }
        foreach (RadioButton tab in new[] { _activeTab, _inactiveTab })
        {
            tab.CheckedChanged += (_, _) =>
            {
                StyleTab(tab);
                if (tab.Checked)
                {
                    ShowStudents();
                }
            };
        }
        ShowTabButtons();

        Load += (_, _) => LoadStudents();
    }

    private ComboBox CreateFilter(int width)
    {
        var combo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = UiTheme.BodyFont,
            Width = width,
            Margin = new Padding(0, 4, 8, 0),
        };
        return combo;
    }

    /// <summary>True on the "Inactive students" tab (students who have checked out).</summary>
    private bool ShowingInactive => _inactiveTab.Checked;

    private static RadioButton CreateTab(string text)
    {
        var tab = new RadioButton
        {
            Text = text,
            Appearance = Appearance.Button,
            AutoSize = false,
            Font = UiTheme.BodyBoldFont,
            TextAlign = ContentAlignment.MiddleCenter,
            FlatStyle = FlatStyle.Flat,
            Height = 40,
            Margin = new Padding(0, 4, 8, 4),
            Cursor = Cursors.Hand,
        };
        tab.Width = TextRenderer.MeasureText(text, tab.Font).Width + 48;
        tab.FlatAppearance.BorderColor = UiTheme.NavSelected;
        StyleTab(tab);
        return tab;
    }

    /// <summary>The chosen tab is filled; the other is outlined.</summary>
    private static void StyleTab(RadioButton tab)
    {
        // Checked: white text on orange; not checked: orange text on white (CheckedBackColor follows, so the
        // text never ends up orange on orange).
        tab.BackColor = tab.Checked ? UiTheme.NavSelected : Color.White;
        tab.ForeColor = tab.Checked ? Color.White : UiTheme.NavSelected;
        tab.FlatAppearance.CheckedBackColor = tab.BackColor;
        tab.FlatAppearance.MouseOverBackColor = tab.Checked ? UiTheme.NavSelected : UiTheme.NavHover;
    }

    /// <summary>Shows the buttons that belong to the selected tab.</summary>
    private void ShowTabButtons()
    {
        bool inactive = ShowingInactive;
        _addButton.Visible = !inactive;
        _deleteButton.Visible = !inactive;
        _signButton.Visible = !inactive;
        _idCardButton.Visible = !inactive;
        _transportCardButton.Visible = !inactive;
        _makeActiveButton.Visible = inactive;
        _deletePermanentlyButton.Visible = inactive;
    }

    private Student? SelectedStudent => _grid.CurrentRow?.DataBoundItem as Student;

    /// <summary>The selected students in list order (the current row when none is highlighted).</summary>
    private List<int> SelectedStudentIds()
    {
        List<int> ids = _grid.SelectedRows.Cast<DataGridViewRow>()
            .OrderBy(r => r.Index)
            .Select(r => r.DataBoundItem)
            .OfType<Student>()
            .Select(s => s.StudentId)
            .ToList();
        if (ids.Count == 0 && SelectedStudent is Student current)
        {
            ids.Add(current.StudentId);
        }
        return ids;
    }

    private void LoadStudents(int? selectStudentId = null)
    {
        try
        {
            _colleges = CollegeService.Search(_hostelId);
            _students = StudentService.GetStudents(_hostelId);

            FillFilter(_collegeFilter, AllColleges, _colleges.Select(c => c.CollegeName));
            FillFilter(_courseFilter, AllCourses, _students.Select(s => s.Course));
            FillFilter(_classFilter, AllClasses, _students.Select(s => s.ClassName));
            ShowStudents(selectStudentId);
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The students could not be loaded.");
        }
    }

    /// <summary>Refills a filter with the distinct values, keeping the current choice when it still exists.</summary>
    private static void FillFilter(ComboBox filter, string allText, IEnumerable<string> values)
    {
        object? current = filter.SelectedItem;
        filter.BeginUpdate();
        filter.Items.Clear();
        filter.Items.Add(allText);
        filter.Items.AddRange(values
            .Where(v => v.Length > 0)
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .Order(StringComparer.CurrentCultureIgnoreCase)
            .Cast<object>()
            .ToArray());
        filter.SelectedItem = current is not null && filter.Items.Contains(current) ? current : allText;
        filter.EndUpdate();
    }

    private void ShowStudents(int? selectStudentId = null)
    {
        ShowTabButtons();
        string status = ShowingInactive ? StudentStatus.Left : StudentStatus.Active;
        string search = _searchBox.Text.Trim();
        IEnumerable<Student> students = _students.Where(s => s.Status == status);

        if (search.Length > 0)
        {
            students = students.Where(s =>
                s.StudentName.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
                s.Mobile.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                s.FatherMobile.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                s.FatherName.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
                s.MotherMobile.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                s.MotherName.Contains(search, StringComparison.CurrentCultureIgnoreCase));
        }
        students = Filter(students, _collegeFilter, AllColleges, s => s.CollegeName);
        students = Filter(students, _courseFilter, AllCourses, s => s.Course);
        students = Filter(students, _classFilter, AllClasses, s => s.ClassName);

        List<Student> shown = students.ToList();
        _grid.DataSource = shown;

        if (selectStudentId is not null)
        {
            int index = shown.FindIndex(s => s.StudentId == selectStudentId);
            if (index >= 0)
            {
                _grid.CurrentCell = _grid.Rows[index].Cells[0];
            }
        }

        int inTab = _students.Count(s => s.Status == status);
        _countLabel.Text = $"Showing {shown.Count} of {inTab} {(ShowingInactive ? "inactive" : "active")} students";
        UpdateButtons();
    }

    private static IEnumerable<Student> Filter(IEnumerable<Student> students, ComboBox filter, string allText,
        Func<Student, string> value) =>
        filter.SelectedItem is string chosen && chosen != allText
            ? students.Where(s => string.Equals(value(s), chosen, StringComparison.CurrentCultureIgnoreCase))
            : students;

    private void UpdateButtons()
    {
        bool hasSelection = SelectedStudent is not null;
        _editButton.Enabled = hasSelection;
        _deleteButton.Enabled = hasSelection;
        _refundButton.Enabled = hasSelection;
        _makeActiveButton.Enabled = hasSelection;
        _deletePermanentlyButton.Enabled = hasSelection;
        _agreementButton.Enabled = hasSelection;
        _signButton.Enabled = hasSelection;
        _idCardButton.Enabled = hasSelection;
        _transportCardButton.Enabled = hasSelection;
    }

    private void AddStudent()
    {
        if (_colleges.Count == 0)
        {
            Dialogs.Warning("Please add the colleges of this hostel first (Colleges in the menu).");
            return;
        }

        using var dialog = new StudentEditForm(_colleges);
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _searchBox.Clear();
            LoadStudents(dialog.SavedStudent?.StudentId);
        }
    }

    private void EditSelectedStudent()
    {
        if (SelectedStudent is not Student selected)
        {
            return;
        }

        try
        {
            Student? student = StudentService.GetStudent(selected.StudentId);
            if (student is null)
            {
                Dialogs.Warning("This student no longer exists.");
                LoadStudents();
                return;
            }

            using var dialog = new StudentEditForm(_colleges, student);
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                LoadStudents(student.StudentId);
            }
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The student could not be opened.");
        }
    }

    private void DeleteSelectedStudent()
    {
        if (SelectedStudent is not Student student ||
            !Dialogs.Confirm($"Delete {student.StudentName} with their photo and Aadhaar card?"))
        {
            return;
        }

        try
        {
            StudentService.Delete(student.StudentId);
            LoadStudents();
        }
        catch (ValidationException ex)
        {
            Dialogs.Warning(ex.Message);
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The student could not be deleted.");
        }
    }

    private void MakeSelectedActive()
    {
        if (SelectedStudent is not Student student ||
            !Dialogs.Confirm($"Make {student.StudentName} active again? A room can then be allocated from Room Allocation."))
        {
            return;
        }

        try
        {
            StudentService.MakeActive(student.StudentId);
            _activeTab.Checked = true;
            LoadStudents(student.StudentId);
        }
        catch (ValidationException ex)
        {
            Dialogs.Warning(ex.Message);
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The student could not be made active.");
        }
    }

    private void DeleteSelectedPermanently()
    {
        if (SelectedStudent is not Student student)
        {
            return;
        }

        using var dialog = new ReasonForm("Delete Student Permanently",
            $"Delete {student.StudentName} permanently? Every record of this student is removed: invoices, payments, " +
            "refunds, room history, attendance, messages, photo, Aadhaar card and signatures. A backup of the database " +
            "is made first. This cannot be undone in the program.",
            "Delete Permanently");
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            string? backup = StudentService.DeletePermanently(student.StudentId, dialog.Reason);
            LoadStudents();
            Dialogs.Info($"{student.StudentName} was deleted permanently." +
                         (backup is null ? "" : $"{Environment.NewLine}The backup made before deleting: {backup}"));
        }
        catch (ValidationException ex)
        {
            Dialogs.Warning(ex.Message);
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The student could not be deleted.");
        }
    }
}
