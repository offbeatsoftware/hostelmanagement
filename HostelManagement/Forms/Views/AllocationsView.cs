using HostelManagement.Models;
using HostelManagement.Services;
using HostelManagement.Utilities;

namespace HostelManagement.Forms.Views;

/// <summary>Who is in which room of the selected hostel, with check-in, transfer, check-out and history.</summary>
public sealed class AllocationsView : UserControl
{
    private const string ShowCurrent = "Students in rooms";
    private const string ShowHistory = "Full history";

    private readonly int _hostelId;
    private readonly TextBox _searchBox;
    private readonly ComboBox _showBox;
    private readonly DataGridView _grid;
    private readonly Button _transferButton;
    private readonly Button _checkOutButton;
    private readonly Label _summaryLabel;

    private List<RoomAllocation> _allocations = [];

    public AllocationsView(Hostel hostel)
    {
        _hostelId = hostel.HostelId;
        Dock = DockStyle.Fill;
        BackColor = UiTheme.ContentBackground;

        _searchBox = new TextBox
        {
            Font = UiTheme.BodyFont,
            Width = 220,
            PlaceholderText = "Search student or room",
            Margin = new Padding(0, 4, 8, 0),
        };
        _searchBox.TextChanged += (_, _) => ShowAllocations();

        _showBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = UiTheme.BodyFont,
            Width = 160,
            Margin = new Padding(0, 4, 16, 0),
        };
        _showBox.Items.AddRange([ShowCurrent, ShowHistory]);
        _showBox.SelectedIndex = 0;
        _showBox.SelectedIndexChanged += (_, _) => LoadAllocations();

        var checkInButton = new Button { Text = "Check-in" };
        UiTheme.StylePrimaryButton(checkInButton);
        checkInButton.Click += (_, _) => CheckIn();

        _transferButton = new Button { Text = "Transfer" };
        UiTheme.StyleSecondaryButton(_transferButton);
        _transferButton.Click += (_, _) => RunForSelected(AllocationAction.Transfer);

        _checkOutButton = new Button { Text = "Check-out" };
        UiTheme.StyleDangerButton(_checkOutButton);
        _checkOutButton.Click += (_, _) => RunForSelected(AllocationAction.CheckOut);

        _summaryLabel = FormFields.CreateMessageLabel();
        _summaryLabel.ForeColor = UiTheme.TextMuted;

        FlowLayoutPanel toolbar = FormFields.CreateButtonRow(
            _searchBox, _showBox, checkInButton, _transferButton, _checkOutButton, _summaryLabel);
        toolbar.Dock = DockStyle.Top;

        _grid = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false };
        UiTheme.StyleGrid(_grid);
        FormFields.AddGridColumn(_grid, nameof(RoomAllocation.RoomNumber), "Room", 8);
        FormFields.AddGridColumn(_grid, nameof(RoomAllocation.StudentName), "Student", 20);
        FormFields.AddGridColumn(_grid, nameof(RoomAllocation.StudentMobile), "Mobile", 11);
        FormFields.AddGridColumn(_grid, nameof(RoomAllocation.CheckInDate), "Check-in", 10, format: "dd MMM yyyy");
        FormFields.AddGridColumn(_grid, nameof(RoomAllocation.CheckOutDate), "Left room", 10, format: "dd MMM yyyy");
        FormFields.AddGridColumn(_grid, nameof(RoomAllocation.Days), "Days", 6, alignRight: true);
        FormFields.AddGridColumn(_grid, nameof(RoomAllocation.StatusText), "Status", 10);
        FormFields.AddGridColumn(_grid, nameof(RoomAllocation.Remarks), "Remarks", 25);
        _grid.SelectionChanged += (_, _) => UpdateButtons();

        var body = new Panel { Dock = DockStyle.Fill };
        body.Controls.Add(_grid);
        body.Controls.Add(toolbar);
        Panel card = FormFields.CreateCard($"Room allocation in {hostel.HostelName}", body);
        card.Dock = DockStyle.Fill;
        Controls.Add(card);

        Load += (_, _) => LoadAllocations();
    }

    private RoomAllocation? SelectedAllocation => _grid.CurrentRow?.DataBoundItem as RoomAllocation;

    private void LoadAllocations()
    {
        try
        {
            _allocations = AllocationService.GetAllocations(_hostelId, includeHistory: _showBox.SelectedItem as string == ShowHistory);
            ShowAllocations();

            int inRooms = _allocations.Count(a => a.Status == AllocationStatus.Current);
            int waiting = AllocationService.GetStudentsWithoutRoom(_hostelId).Count;
            _summaryLabel.Text = $"{inRooms} students in rooms, {waiting} active students without a room";
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The room allocations could not be loaded.");
        }
    }

    private void ShowAllocations()
    {
        string search = _searchBox.Text.Trim();
        _grid.DataSource = search.Length == 0
            ? _allocations
            : _allocations.Where(a =>
                a.StudentName.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
                a.RoomNumber.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
                a.StudentMobile.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
        UpdateButtons();
    }

    /// <summary>Transfer and check-out work on a student who is in a room now.</summary>
    private void UpdateButtons()
    {
        bool current = SelectedAllocation?.Status == AllocationStatus.Current;
        _transferButton.Enabled = current;
        _checkOutButton.Enabled = current;
    }

    private void CheckIn()
    {
        try
        {
            List<Student> students = AllocationService.GetStudentsWithoutRoom(_hostelId);
            if (students.Count == 0)
            {
                Dialogs.Info("All active students of this hostel are already in a room.");
                return;
            }

            using var dialog = new AllocationForm(AllocationAction.CheckIn, _hostelId, students);
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                LoadAllocations();
            }
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "Check-in could not be started.");
        }
    }

    private void RunForSelected(AllocationAction action)
    {
        if (SelectedAllocation is not { Status: AllocationStatus.Current } allocation)
        {
            return;
        }

        using var dialog = new AllocationForm(action, _hostelId, current: allocation);
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            LoadAllocations();
        }
    }
}
