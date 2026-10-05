using HostelManagement.Models;
using HostelManagement.Services;
using HostelManagement.Utilities;

namespace HostelManagement.Forms.Views;

/// <summary>Parents and guardians of the selected hostel's students.</summary>
public sealed class ParentsView : UserControl
{
    private readonly int _hostelId;
    private readonly TextBox _searchBox;
    private readonly DataGridView _grid;
    private readonly Button _editButton;
    private readonly Button _deleteButton;

    private List<Parent> _parents = [];

    public ParentsView(Hostel hostel)
    {
        _hostelId = hostel.HostelId;
        Dock = DockStyle.Fill;
        BackColor = UiTheme.ContentBackground;

        _searchBox = new TextBox
        {
            Font = UiTheme.BodyFont,
            Width = 260,
            PlaceholderText = "Search student, parent, mobile or email",
            Margin = new Padding(0, 4, 16, 0),
        };
        _searchBox.TextChanged += (_, _) => ShowParents();

        var addButton = new Button { Text = "Add" };
        UiTheme.StylePrimaryButton(addButton);
        addButton.Click += (_, _) => AddParent();

        _editButton = new Button { Text = "Edit" };
        UiTheme.StyleSecondaryButton(_editButton);
        _editButton.Click += (_, _) => EditSelectedParent();

        _deleteButton = new Button { Text = "Delete" };
        UiTheme.StyleDangerButton(_deleteButton);
        _deleteButton.Click += (_, _) => DeleteSelectedParent();

        FlowLayoutPanel toolbar = FormFields.CreateButtonRow(_searchBox, addButton, _editButton, _deleteButton);
        toolbar.Dock = DockStyle.Top;

        _grid = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false };
        UiTheme.StyleGrid(_grid);
        FormFields.AddGridColumn(_grid, nameof(Models.Parent.StudentName), "Student", 18);
        FormFields.AddGridColumn(_grid, nameof(Models.Parent.ParentName), "Parent / guardian", 18);
        FormFields.AddGridColumn(_grid, nameof(Models.Parent.Relationship), "Relationship", 10);
        FormFields.AddGridColumn(_grid, nameof(Models.Parent.Mobile), "Mobile", 12);
        FormFields.AddGridColumn(_grid, nameof(Models.Parent.Email), "Email", 20);
        FormFields.AddGridColumn(_grid, nameof(Models.Parent.PrimaryText), "Primary", 7);
        _grid.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0)
            {
                EditSelectedParent();
            }
        };
        _grid.SelectionChanged += (_, _) => UpdateButtons();

        var body = new Panel { Dock = DockStyle.Fill };
        body.Controls.Add(_grid);
        body.Controls.Add(toolbar);
        Panel card = FormFields.CreateCard($"Parents and guardians of {hostel.HostelName} students", body);
        card.Dock = DockStyle.Fill;
        Controls.Add(card);

        Load += (_, _) => LoadParents();
    }

    private Parent? SelectedParent => _grid.CurrentRow?.DataBoundItem as Parent;

    private void LoadParents(int? selectParentId = null)
    {
        try
        {
            _parents = ParentService.GetParents(_hostelId);
            ShowParents(selectParentId);
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The parents could not be loaded.");
        }
    }

    private void ShowParents(int? selectParentId = null)
    {
        string search = _searchBox.Text.Trim();
        List<Parent> shown = search.Length == 0
            ? _parents
            : _parents.Where(p =>
                p.StudentName.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
                p.ParentName.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
                p.Mobile.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                p.Email.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();

        _grid.DataSource = shown;
        if (selectParentId is not null)
        {
            int index = shown.FindIndex(p => p.ParentId == selectParentId);
            if (index >= 0)
            {
                _grid.CurrentCell = _grid.Rows[index].Cells[0];
            }
        }
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        bool hasSelection = SelectedParent is not null;
        _editButton.Enabled = hasSelection;
        _deleteButton.Enabled = hasSelection;
    }

    private void AddParent()
    {
        try
        {
            List<Student> students = StudentService.GetStudents(_hostelId);
            if (students.Count == 0)
            {
                Dialogs.Warning("Please add students first. Each student's main parent is entered with the student.");
                return;
            }

            using var dialog = new ParentEditForm(students);
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                _searchBox.Clear();
                LoadParents(dialog.SavedParent?.ParentId);
            }
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The parent could not be added.");
        }
    }

    private void EditSelectedParent()
    {
        if (SelectedParent is not Parent parent)
        {
            return;
        }

        using var dialog = new ParentEditForm([], parent);
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            LoadParents(parent.ParentId);
        }
    }

    private void DeleteSelectedParent()
    {
        if (SelectedParent is not Parent parent ||
            !Dialogs.Confirm($"Delete {parent.ParentName} ({parent.StudentName}'s parent or guardian)?"))
        {
            return;
        }

        try
        {
            ParentService.Delete(parent.ParentId);
            LoadParents();
        }
        catch (ValidationException ex)
        {
            Dialogs.Warning(ex.Message);
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The parent could not be deleted.");
        }
    }
}
