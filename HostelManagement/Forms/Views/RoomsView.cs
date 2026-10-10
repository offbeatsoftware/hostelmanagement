using HostelManagement.Models;
using HostelManagement.Services;
using HostelManagement.Utilities;

namespace HostelManagement.Forms.Views;

/// <summary>
/// The rooms of the selected hostel with occupancy. A room's sharing type only decides how many students fit in
/// it; the rent is agreed per student at check-in (client decision, version 1.2).
/// </summary>
public sealed class RoomsView : UserControl
{
    private readonly int _hostelId;

    private const string FilterAll = "All rooms";
    private const string FilterActive = "Active rooms";
    private const string FilterInactive = "Inactive rooms";
    private const string FilterFreeBeds = "Rooms with free beds";

    private readonly TextBox _searchBox;
    private readonly ComboBox _filterBox;
    private readonly DataGridView _roomGrid;
    private readonly Button _editButton;
    private readonly Button _deleteButton;
    private readonly Label _summaryLabel;

    private List<SharingType> _sharingTypes = [];
    private List<Room> _rooms = [];

    public RoomsView(Hostel hostel)
    {
        _hostelId = hostel.HostelId;
        Dock = DockStyle.Fill;
        BackColor = UiTheme.ContentBackground;

        // ---- Rooms ----
        _searchBox = new TextBox
        {
            Font = UiTheme.BodyFont,
            Width = 220,
            PlaceholderText = "Search room number or floor",
            Margin = new Padding(0, 4, 8, 0),
        };
        _searchBox.TextChanged += (_, _) => ShowRooms();

        _filterBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = UiTheme.BodyFont,
            Width = 180,
            Margin = new Padding(0, 4, 16, 0),
        };
        _filterBox.Items.AddRange([FilterAll, FilterActive, FilterInactive, FilterFreeBeds]);
        _filterBox.SelectedIndex = 0;
        _filterBox.SelectedIndexChanged += (_, _) => ShowRooms();

        var addButton = new Button { Text = "Add" };
        UiTheme.StylePrimaryButton(addButton);
        addButton.Click += (_, _) => AddRoom();

        _editButton = new Button { Text = "Edit" };
        UiTheme.StyleSecondaryButton(_editButton);
        _editButton.Click += (_, _) => EditSelectedRoom();

        _deleteButton = new Button { Text = "Delete" };
        UiTheme.StyleDangerButton(_deleteButton);
        _deleteButton.Click += (_, _) => DeleteSelectedRoom();

        _summaryLabel = FormFields.CreateMessageLabel();
        _summaryLabel.ForeColor = UiTheme.TextMuted;

        FlowLayoutPanel roomToolbar = FormFields.CreateButtonRow(
            _searchBox, _filterBox, addButton, _editButton, _deleteButton, _summaryLabel);
        roomToolbar.Dock = DockStyle.Top;

        _roomGrid = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false };
        UiTheme.StyleGrid(_roomGrid);
        FormFields.AddGridColumn(_roomGrid, nameof(Room.RoomNumber), "Room", 10);
        FormFields.AddGridColumn(_roomGrid, nameof(Room.Floor), "Floor", 10);
        FormFields.AddGridColumn(_roomGrid, nameof(Room.RoomFor), "For", 7);
        FormFields.AddGridColumn(_roomGrid, nameof(Room.SharingName), "Sharing", 10);
        FormFields.AddGridColumn(_roomGrid, nameof(Room.Capacity), "Capacity", 8, alignRight: true);
        FormFields.AddGridColumn(_roomGrid, nameof(Room.Occupied), "Occupied", 8, alignRight: true);
        FormFields.AddGridColumn(_roomGrid, nameof(Room.Available), "Available", 8, alignRight: true);
        FormFields.AddGridColumn(_roomGrid, nameof(Room.Status), "Status", 9);
        FormFields.AddGridColumn(_roomGrid, nameof(Room.Remarks), "Remarks", 37);
        _roomGrid.CellFormatting += FormatRoomCell;
        _roomGrid.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0)
            {
                EditSelectedRoom();
            }
        };
        _roomGrid.SelectionChanged += (_, _) => UpdateRoomButtons();

        var roomBody = new Panel { Dock = DockStyle.Fill };
        roomBody.Controls.Add(_roomGrid);
        roomBody.Controls.Add(roomToolbar);
        Panel roomCard = FormFields.CreateCard($"Rooms of {hostel.HostelName}", roomBody);
        roomCard.Dock = DockStyle.Fill;

        Controls.Add(roomCard);

        Load += (_, _) => LoadData();
    }

    private void LoadData()
    {
        try
        {
            _sharingTypes = RoomService.GetSharingTypes(_hostelId);
            LoadRooms();
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The rooms could not be loaded.");
        }
    }

    private void LoadRooms(int? selectRoomId = null)
    {
        try
        {
            _rooms = RoomService.GetRooms(_hostelId);
            ShowRooms(selectRoomId);
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The room list could not be loaded.");
        }
    }

    /// <summary>Applies the search box and filter to the loaded rooms.</summary>
    private void ShowRooms(int? selectRoomId = null)
    {
        string search = _searchBox.Text.Trim();
        IEnumerable<Room> rooms = _rooms;

        if (search.Length > 0)
        {
            rooms = rooms.Where(room =>
                room.RoomNumber.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                room.Floor.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        rooms = (_filterBox.SelectedItem as string) switch
        {
            FilterActive => rooms.Where(room => room.IsActive),
            FilterInactive => rooms.Where(room => !room.IsActive),
            FilterFreeBeds => rooms.Where(room => room.Available > 0),
            _ => rooms,
        };

        List<Room> shown = rooms.ToList();
        _roomGrid.DataSource = shown;

        if (selectRoomId is not null)
        {
            int index = shown.FindIndex(room => room.RoomId == selectRoomId);
            if (index >= 0)
            {
                _roomGrid.CurrentCell = _roomGrid.Rows[index].Cells[0];
            }
        }

        List<Room> active = _rooms.Where(room => room.IsActive).ToList();
        _summaryLabel.Text =
            $"{active.Count} active rooms, {active.Sum(r => r.Capacity)} beds, " +
            $"{active.Sum(r => r.Occupied)} occupied, {active.Sum(r => r.Available)} free";

        UpdateRoomButtons();
    }

    private void FormatRoomCell(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || _roomGrid.Rows[e.RowIndex].DataBoundItem is not Room room)
        {
            return;
        }

        if (!room.IsActive)
        {
            e.CellStyle!.ForeColor = UiTheme.TextMuted;
        }
        else if (_roomGrid.Columns[e.ColumnIndex].Name == nameof(Room.Available) && room.Available == 0)
        {
            e.CellStyle!.ForeColor = UiTheme.Danger;
        }
    }

    private Room? SelectedRoom => _roomGrid.CurrentRow?.DataBoundItem as Room;

    private void UpdateRoomButtons()
    {
        bool hasSelection = SelectedRoom is not null;
        _editButton.Enabled = hasSelection;
        _deleteButton.Enabled = hasSelection;
    }

    private void AddRoom()
    {
        using var dialog = new RoomEditForm(_hostelId, _sharingTypes);
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _searchBox.Clear();
            _filterBox.SelectedItem = FilterAll;
            LoadRooms(dialog.SavedRoom?.RoomId);
        }
    }

    private void EditSelectedRoom()
    {
        if (SelectedRoom is not Room room)
        {
            return;
        }

        using var dialog = new RoomEditForm(_hostelId, _sharingTypes, room);
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            LoadRooms(room.RoomId);
        }
    }

    private void DeleteSelectedRoom()
    {
        if (SelectedRoom is not Room room || !Dialogs.Confirm($"Delete room {room.RoomNumber}?"))
        {
            return;
        }

        try
        {
            RoomService.Delete(room.RoomId);
            LoadRooms();
        }
        catch (ValidationException ex)
        {
            Dialogs.Warning(ex.Message);
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The room could not be deleted.");
        }
    }
}
