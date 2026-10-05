using HostelManagement.Models;
using HostelManagement.Services;
using HostelManagement.Utilities;

namespace HostelManagement.Forms.Views;

/// <summary>Colleges of the selected hostel, with search, add, edit and delete.</summary>
public sealed class CollegesView : UserControl
{
    private readonly int _hostelId;
    private readonly TextBox _searchBox;
    private readonly DataGridView _collegeGrid;
    private readonly Button _editButton;
    private readonly Button _deleteButton;

    public CollegesView(Hostel hostel)
    {
        _hostelId = hostel.HostelId;
        Dock = DockStyle.Fill;
        BackColor = UiTheme.ContentBackground;

        _searchBox = new TextBox
        {
            Font = UiTheme.BodyFont,
            Width = 240,
            PlaceholderText = "Search college name",
            Margin = new Padding(0, 4, 16, 0),
        };
        _searchBox.TextChanged += (_, _) => LoadColleges();

        var addButton = new Button { Text = "Add" };
        UiTheme.StylePrimaryButton(addButton);
        addButton.Click += (_, _) => AddCollege();

        _editButton = new Button { Text = "Edit" };
        UiTheme.StyleSecondaryButton(_editButton);
        _editButton.Click += (_, _) => EditSelectedCollege();

        _deleteButton = new Button { Text = "Delete" };
        UiTheme.StyleDangerButton(_deleteButton);
        _deleteButton.Click += (_, _) => DeleteSelectedCollege();

        FlowLayoutPanel toolbar = FormFields.CreateButtonRow(_searchBox, addButton, _editButton, _deleteButton);
        toolbar.Dock = DockStyle.Top;

        _collegeGrid = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false };
        UiTheme.StyleGrid(_collegeGrid);
        FormFields.AddGridColumn(_collegeGrid, nameof(College.CollegeName), "College", 35);
        FormFields.AddGridColumn(_collegeGrid, nameof(College.Address), "Address", 45);
        FormFields.AddGridColumn(_collegeGrid, nameof(College.Phone), "Phone", 20);
        _collegeGrid.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0)
            {
                EditSelectedCollege();
            }
        };
        _collegeGrid.SelectionChanged += (_, _) => UpdateButtons();

        var body = new Panel { Dock = DockStyle.Fill };
        body.Controls.Add(_collegeGrid);
        body.Controls.Add(toolbar);
        Panel card = FormFields.CreateCard($"Colleges of {hostel.HostelName}", body);
        card.Dock = DockStyle.Fill;
        Controls.Add(card);

        Load += (_, _) => LoadColleges();
    }

    private College? SelectedCollege => _collegeGrid.CurrentRow?.DataBoundItem as College;

    private void LoadColleges(int? selectCollegeId = null)
    {
        try
        {
            List<College> colleges = CollegeService.Search(_hostelId, _searchBox.Text);
            _collegeGrid.DataSource = colleges;

            if (selectCollegeId is not null)
            {
                int index = colleges.FindIndex(c => c.CollegeId == selectCollegeId);
                if (index >= 0)
                {
                    _collegeGrid.CurrentCell = _collegeGrid.Rows[index].Cells[0];
                }
            }
            UpdateButtons();
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The college list could not be loaded.");
        }
    }

    private void UpdateButtons()
    {
        bool hasSelection = SelectedCollege is not null;
        _editButton.Enabled = hasSelection;
        _deleteButton.Enabled = hasSelection;
    }

    private void AddCollege()
    {
        using var dialog = new CollegeEditForm(_hostelId);
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _searchBox.Clear();
            LoadColleges(dialog.SavedCollege?.CollegeId);
        }
    }

    private void EditSelectedCollege()
    {
        if (SelectedCollege is not College college)
        {
            return;
        }

        using var dialog = new CollegeEditForm(college.HostelId, college);
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            LoadColleges(college.CollegeId);
        }
    }

    private void DeleteSelectedCollege()
    {
        if (SelectedCollege is not College college ||
            !Dialogs.Confirm($"Delete the college \"{college.CollegeName}\"?"))
        {
            return;
        }

        try
        {
            CollegeService.Delete(college.CollegeId);
            LoadColleges();
        }
        catch (ValidationException ex)
        {
            Dialogs.Warning(ex.Message);
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The college could not be deleted.");
        }
    }
}
