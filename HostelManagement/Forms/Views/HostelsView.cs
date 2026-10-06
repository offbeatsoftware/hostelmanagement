using HostelManagement.Models;
using HostelManagement.Services;
using HostelManagement.Utilities;

namespace HostelManagement.Forms.Views;

/// <summary>List of all hostels with add, edit and delete.</summary>
public sealed class HostelsView : UserControl
{
    private readonly DataGridView _hostelGrid;
    private readonly Button _editButton;
    private readonly Button _deleteButton;
    private readonly Label _hintLabel;

    public HostelsView()
    {
        Dock = DockStyle.Fill;
        BackColor = UiTheme.ContentBackground;

        var addButton = new Button { Text = "Add" };
        UiTheme.StylePrimaryButton(addButton);
        addButton.Click += (_, _) => AddHostel();

        _editButton = new Button { Text = "Edit" };
        UiTheme.StyleSecondaryButton(_editButton);
        _editButton.Click += (_, _) => EditSelectedHostel();

        _deleteButton = new Button { Text = "Delete" };
        UiTheme.StyleDangerButton(_deleteButton);
        _deleteButton.Click += (_, _) => DeleteSelectedHostel();

        _hintLabel = FormFields.CreateMessageLabel();
        _hintLabel.ForeColor = UiTheme.TextMuted;

        FlowLayoutPanel toolbar = FormFields.CreateButtonRow(addButton, _editButton, _deleteButton, _hintLabel);
        toolbar.Dock = DockStyle.Top;

        _hostelGrid = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false };
        UiTheme.StyleGrid(_hostelGrid);
        FormFields.AddGridColumn(_hostelGrid, nameof(Hostel.HostelName), "Hostel", 25);
        FormFields.AddGridColumn(_hostelGrid, nameof(Hostel.Address), "Address", 30);
        FormFields.AddGridColumn(_hostelGrid, nameof(Hostel.Phone), "Phone", 12);
        FormFields.AddGridColumn(_hostelGrid, nameof(Hostel.Email), "Email", 18);
        FormFields.AddGridColumn(_hostelGrid, nameof(Hostel.BillingText), "Billing", 11);
        FormFields.AddGridColumn(_hostelGrid, nameof(Hostel.CollegeCount), "Colleges", 7, alignRight: true);
        FormFields.AddGridColumn(_hostelGrid, nameof(Hostel.RoomCount), "Rooms", 7, alignRight: true);
        _hostelGrid.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0)
            {
                EditSelectedHostel();
            }
        };
        _hostelGrid.SelectionChanged += (_, _) => UpdateButtons();

        var body = new Panel { Dock = DockStyle.Fill };
        body.Controls.Add(_hostelGrid);
        body.Controls.Add(toolbar);
        Panel card = FormFields.CreateCard("Hostels", body);
        card.Dock = DockStyle.Fill;
        Controls.Add(card);

        Load += (_, _) => LoadHostels();
    }

    private Hostel? SelectedHostel => _hostelGrid.CurrentRow?.DataBoundItem as Hostel;

    private void LoadHostels(int? selectHostelId = null)
    {
        try
        {
            List<Hostel> hostels = HostelService.GetHostels();
            _hostelGrid.DataSource = hostels;

            int index = hostels.FindIndex(h => h.HostelId == (selectHostelId ?? HostelContext.CurrentHostelId));
            if (index >= 0)
            {
                _hostelGrid.CurrentCell = _hostelGrid.Rows[index].Cells[0];
            }

            _hintLabel.Text = hostels.Count == 0
                ? "Add your first hostel to get started."
                : "Select the hostel to work on in the box at the top right.";
            UpdateButtons();
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The hostels could not be loaded.");
        }
    }

    private void UpdateButtons()
    {
        bool hasSelection = SelectedHostel is not null;
        _editButton.Enabled = hasSelection;
        _deleteButton.Enabled = hasSelection;
    }

    private void AddHostel()
    {
        using var dialog = new HostelEditForm();
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            LoadHostels(dialog.SavedHostel?.HostelId);
        }
    }

    private void EditSelectedHostel()
    {
        if (SelectedHostel is not Hostel hostel)
        {
            return;
        }

        using var dialog = new HostelEditForm(hostel);
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            LoadHostels(hostel.HostelId);
        }
    }

    private void DeleteSelectedHostel()
    {
        if (SelectedHostel is not Hostel hostel || !Dialogs.Confirm($"Delete the hostel \"{hostel.HostelName}\"?"))
        {
            return;
        }

        try
        {
            HostelService.Delete(hostel.HostelId);
            LoadHostels();
        }
        catch (ValidationException ex)
        {
            Dialogs.Warning(ex.Message);
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The hostel could not be deleted.");
        }
    }
}
