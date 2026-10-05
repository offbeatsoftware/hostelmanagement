using HostelManagement.Models;
using HostelManagement.Services;
using HostelManagement.Utilities;

namespace HostelManagement.Forms.Views;

/// <summary>Hostel details (top) and the list of colleges students attend (bottom).</summary>
public sealed class HostelDetailsView : UserControl
{
    private readonly TextBox _nameBox;
    private readonly TextBox _addressBox;
    private readonly TextBox _phoneBox;
    private readonly TextBox _emailBox;
    private readonly Label _hostelMessage;

    private readonly TextBox _searchBox;
    private readonly DataGridView _collegeGrid;
    private readonly Button _editButton;
    private readonly Button _deleteButton;

    public HostelDetailsView()
    {
        Dock = DockStyle.Fill;
        BackColor = UiTheme.ContentBackground;

        // ---- Hostel details ----
        var hostelFields = FormFields.CreateTable();
        _nameBox = FormFields.AddTextBox(hostelFields, "Hostel name", 150, required: true);
        _addressBox = FormFields.AddTextBox(hostelFields, "Address", 255, multiline: true);
        _phoneBox = FormFields.AddTextBox(hostelFields, "Phone", 20);
        _emailBox = FormFields.AddTextBox(hostelFields, "Email", 150);

        var saveButton = new Button { Text = "Save" };
        UiTheme.StylePrimaryButton(saveButton);
        saveButton.Click += (_, _) => SaveHostel();
        _hostelMessage = FormFields.CreateMessageLabel();

        var hostelButtons = FormFields.CreateButtonRow(saveButton, _hostelMessage);
        var hostelBody = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            Dock = DockStyle.Top,
        };
        hostelBody.Controls.Add(hostelFields);
        hostelBody.Controls.Add(hostelButtons);
        Panel hostelCard = FormFields.CreateCard("Hostel", hostelBody);
        hostelCard.Dock = DockStyle.Top;
        hostelCard.AutoSize = true;

        // ---- Colleges ----
        _searchBox = new TextBox { Font = UiTheme.BodyFont, Width = 240, PlaceholderText = "Search college name" };
        _searchBox.Margin = new Padding(0, 4, 16, 0);
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

        var collegeToolbar = FormFields.CreateButtonRow(_searchBox, addButton, _editButton, _deleteButton);
        collegeToolbar.Dock = DockStyle.Top;

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
        _collegeGrid.SelectionChanged += (_, _) => UpdateCollegeButtons();

        var collegeBody = new Panel { Dock = DockStyle.Fill };
        collegeBody.Controls.Add(_collegeGrid);
        collegeBody.Controls.Add(collegeToolbar);
        Panel collegeCard = FormFields.CreateCard("Colleges", collegeBody);
        collegeCard.Dock = DockStyle.Fill;

        var spacer = new Panel { Dock = DockStyle.Top, Height = 16 };

        // Docked controls are laid out in reverse order of adding.
        Controls.Add(collegeCard);
        Controls.Add(spacer);
        Controls.Add(hostelCard);

        Load += (_, _) => LoadData();
    }

    private void LoadData()
    {
        try
        {
            HostelDetails? hostel = HostelService.GetDetails();
            if (hostel is null)
            {
                _hostelMessage.ForeColor = UiTheme.TextMuted;
                _hostelMessage.Text = "Enter the hostel details and click Save.";
            }
            else
            {
                ShowHostel(hostel);
            }
            LoadColleges();
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The hostel details could not be loaded.");
        }
    }

    private void ShowHostel(HostelDetails hostel)
    {
        _nameBox.Text = hostel.HostelName;
        _addressBox.Text = hostel.Address;
        _phoneBox.Text = hostel.Phone;
        _emailBox.Text = hostel.Email;
    }

    private void SaveHostel()
    {
        try
        {
            HostelDetails saved = HostelService.Save(new HostelDetails
            {
                HostelName = _nameBox.Text,
                Address = _addressBox.Text,
                Phone = _phoneBox.Text,
                Email = _emailBox.Text,
            });
            ShowHostel(saved);
            FormFields.ShowSuccess(_hostelMessage, $"Saved at {DateTime.Now:HH:mm}.");
        }
        catch (ValidationException ex)
        {
            FormFields.ShowError(_hostelMessage, ex.Message);
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The hostel details could not be saved.");
        }
    }

    private void LoadColleges(int? selectCollegeId = null)
    {
        try
        {
            List<College> colleges = CollegeService.Search(_searchBox.Text);
            _collegeGrid.DataSource = colleges;

            if (selectCollegeId is not null)
            {
                int index = colleges.FindIndex(c => c.CollegeId == selectCollegeId);
                if (index >= 0)
                {
                    _collegeGrid.CurrentCell = _collegeGrid.Rows[index].Cells[0];
                }
            }
            UpdateCollegeButtons();
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The college list could not be loaded.");
        }
    }

    private College? SelectedCollege =>
        _collegeGrid.CurrentRow?.DataBoundItem as College;

    private void UpdateCollegeButtons()
    {
        bool hasSelection = SelectedCollege is not null;
        _editButton.Enabled = hasSelection;
        _deleteButton.Enabled = hasSelection;
    }

    private void AddCollege()
    {
        using var dialog = new CollegeEditForm();
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

        using var dialog = new CollegeEditForm(college);
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
