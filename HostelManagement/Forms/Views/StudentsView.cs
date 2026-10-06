using HostelManagement.Models;
using HostelManagement.Services;
using HostelManagement.Utilities;

namespace HostelManagement.Forms.Views;

/// <summary>Students of the selected hostel with search, filters, add, edit and delete.</summary>
public sealed class StudentsView : UserControl
{
    private const string AllColleges = "All colleges";
    private const string AllCourses = "All courses";
    private const string AllClasses = "All classes";
    private const string AllStatuses = "All statuses";

    private readonly int _hostelId;
    private readonly TextBox _searchBox;
    private readonly ComboBox _collegeFilter;
    private readonly ComboBox _courseFilter;
    private readonly ComboBox _classFilter;
    private readonly ComboBox _statusFilter;
    private readonly DataGridView _grid;
    private readonly Button _editButton;
    private readonly Button _deleteButton;
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
            PlaceholderText = "Search name or mobile (student or parent)",
            Margin = new Padding(0, 4, 8, 0),
        };
        _searchBox.TextChanged += (_, _) => ShowStudents();

        _collegeFilter = CreateFilter(180);
        _courseFilter = CreateFilter(150);
        _classFilter = CreateFilter(120);
        _statusFilter = CreateFilter(120);
        _statusFilter.Items.AddRange([AllStatuses, .. StudentStatus.All]);
        _statusFilter.SelectedItem = StudentStatus.Active;

        FlowLayoutPanel filterRow = FormFields.CreateButtonRow(
            _searchBox, _collegeFilter, _courseFilter, _classFilter, _statusFilter);
        filterRow.Dock = DockStyle.Top;

        var addButton = new Button { Text = "Add" };
        UiTheme.StylePrimaryButton(addButton);
        addButton.Click += (_, _) => AddStudent();

        _editButton = new Button { Text = "View / Edit" };
        UiTheme.StyleSecondaryButton(_editButton);
        _editButton.Width = 120;
        _editButton.Click += (_, _) => EditSelectedStudent();

        _deleteButton = new Button { Text = "Delete" };
        UiTheme.StyleDangerButton(_deleteButton);
        _deleteButton.Click += (_, _) => DeleteSelectedStudent();

        _countLabel = FormFields.CreateMessageLabel();
        _countLabel.ForeColor = UiTheme.TextMuted;

        FlowLayoutPanel buttonRow = FormFields.CreateButtonRow(addButton, _editButton, _deleteButton, _countLabel);
        buttonRow.Dock = DockStyle.Top;

        _grid = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false };
        UiTheme.StyleGrid(_grid);
        FormFields.AddGridColumn(_grid, nameof(Student.StudentName), "Student", 16);
        FormFields.AddGridColumn(_grid, nameof(Student.Mobile), "Mobile", 10);
        FormFields.AddGridColumn(_grid, nameof(Student.RoomNumber), "Room", 6);
        FormFields.AddGridColumn(_grid, nameof(Student.CollegeName), "College", 14);
        FormFields.AddGridColumn(_grid, nameof(Student.Course), "Course", 10);
        FormFields.AddGridColumn(_grid, nameof(Student.ClassName), "Class", 7);
        FormFields.AddGridColumn(_grid, nameof(Student.ParentName), "Parent", 12);
        FormFields.AddGridColumn(_grid, nameof(Student.ParentMobile), "Parent mobile", 10);
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
        Panel card = FormFields.CreateCard($"Students of {hostel.HostelName}", body);
        card.Dock = DockStyle.Fill;
        Controls.Add(card);

        // Connected last: setting the default filter above must not refresh a grid that does not exist yet.
        foreach (ComboBox filter in new[] { _collegeFilter, _courseFilter, _classFilter, _statusFilter })
        {
            filter.SelectedIndexChanged += (_, _) => ShowStudents();
        }

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

    private Student? SelectedStudent => _grid.CurrentRow?.DataBoundItem as Student;

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
        if (_statusFilter.SelectedItem is null)
        {
            return;
        }

        string search = _searchBox.Text.Trim();
        IEnumerable<Student> students = _students;

        if (search.Length > 0)
        {
            students = students.Where(s =>
                s.StudentName.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
                s.Mobile.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                s.ParentMobile.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                s.ParentName.Contains(search, StringComparison.CurrentCultureIgnoreCase));
        }
        students = Filter(students, _collegeFilter, AllColleges, s => s.CollegeName);
        students = Filter(students, _courseFilter, AllCourses, s => s.Course);
        students = Filter(students, _classFilter, AllClasses, s => s.ClassName);
        students = Filter(students, _statusFilter, AllStatuses, s => s.Status);

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

        _countLabel.Text = $"Showing {shown.Count} of {_students.Count} students";
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

            using var dialog = new StudentEditForm(_colleges, student, StudentService.GetPrimaryParent(student.StudentId));
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
            !Dialogs.Confirm($"Delete {student.StudentName} with their parent details, photo and Aadhaar card?"))
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
}
