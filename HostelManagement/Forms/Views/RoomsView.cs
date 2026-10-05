using HostelManagement.Models;
using HostelManagement.Services;
using HostelManagement.Utilities;

namespace HostelManagement.Forms.Views;

/// <summary>Rent per sharing type (top) and the room list with occupancy (bottom).</summary>
public sealed class RoomsView : UserControl
{
    private const string FilterAll = "All rooms";
    private const string FilterActive = "Active rooms";
    private const string FilterInactive = "Inactive rooms";
    private const string FilterFreeBeds = "Rooms with free beds";

    private readonly TableLayoutPanel _rentFields;
    private readonly Dictionary<int, TextBox> _rentBoxes = new();
    private readonly Label _rentMessage;

    private readonly TextBox _searchBox;
    private readonly ComboBox _filterBox;
    private readonly DataGridView _roomGrid;
    private readonly Button _editButton;
    private readonly Button _deleteButton;
    private readonly Label _summaryLabel;

    private List<SharingType> _sharingTypes = [];
    private List<Room> _rooms = [];

    public RoomsView()
    {
        Dock = DockStyle.Fill;
        BackColor = UiTheme.ContentBackground;

        // ---- Rent by sharing type ----
        _rentFields = FormFields.CreateTable(labelWidth: 160, inputWidth: 160);
        var saveRentButton = new Button { Text = "Save Rent" };
        UiTheme.StylePrimaryButton(saveRentButton);
        saveRentButton.Click += (_, _) => SaveRent();
        _rentMessage = FormFields.CreateMessageLabel();

        var rentBody = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            Dock = DockStyle.Top,
        };
        rentBody.Controls.Add(_rentFields);
        rentBody.Controls.Add(FormFields.CreateButtonRow(saveRentButton, _rentMessage));
        Panel rentCard = FormFields.CreateCard("Rent per student by sharing type", rentBody);
        rentCard.Dock = DockStyle.Top;
        rentCard.AutoSize = true;

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
        FormFields.AddGridColumn(_roomGrid, nameof(Room.SharingName), "Sharing", 10);
        FormFields.AddGridColumn(_roomGrid, nameof(Room.Capacity), "Capacity", 8, alignRight: true);
        FormFields.AddGridColumn(_roomGrid, nameof(Room.Occupied), "Occupied", 8, alignRight: true);
        FormFields.AddGridColumn(_roomGrid, nameof(Room.Available), "Available", 8, alignRight: true);
        FormFields.AddGridColumn(_roomGrid, nameof(Room.Rent), "Rent / student", 12, format: "C2", alignRight: true);
        FormFields.AddGridColumn(_roomGrid, nameof(Room.Status), "Status", 9);
        FormFields.AddGridColumn(_roomGrid, nameof(Room.Remarks), "Remarks", 25);
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
        Panel roomCard = FormFields.CreateCard("Rooms", roomBody);
        roomCard.Dock = DockStyle.Fill;

        // Docked controls are laid out in reverse order of adding.
        Controls.Add(roomCard);
        Controls.Add(new Panel { Dock = DockStyle.Top, Height = 16 });
        Controls.Add(rentCard);

        Load += (_, _) => LoadData();
    }

    private void LoadData()
    {
        try
        {
            _sharingTypes = RoomService.GetSharingTypes();
            BuildRentFields();
            LoadRooms();
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The rooms could not be loaded.");
        }
    }

    private void BuildRentFields()
    {
        _rentFields.SuspendLayout();
        _rentFields.Controls.Clear();
        _rentFields.RowStyles.Clear();
        _rentFields.RowCount = 0;
        _rentBoxes.Clear();

        foreach (SharingType type in _sharingTypes)
        {
            TextBox box = FormFields.AddTextBox(_rentFields, type.DisplayName, 15);
            box.TextAlign = HorizontalAlignment.Right;
            box.Text = type.Rent.ToString("N2", Money.Culture);
            _rentBoxes[type.SharingTypeId] = box;
        }
        _rentFields.ResumeLayout();

        if (_sharingTypes.Any(type => type.Rent == 0))
        {
            _rentMessage.ForeColor = UiTheme.TextMuted;
            _rentMessage.Text = "Enter the rent for each sharing type and click Save Rent.";
        }
    }

    private void SaveRent()
    {
        // Check every value first so nothing is saved when one of them is wrong.
        var newRents = new Dictionary<int, decimal>();
        foreach (SharingType type in _sharingTypes)
        {
            if (!Money.TryParse(_rentBoxes[type.SharingTypeId].Text, out decimal rent) || rent <= 0)
            {
                FormFields.ShowError(_rentMessage, $"Please enter a valid rent for {type.SharingName} sharing.");
                _rentBoxes[type.SharingTypeId].Focus();
                return;
            }
            newRents[type.SharingTypeId] = rent;
        }

        try
        {
            foreach ((int sharingTypeId, decimal rent) in newRents)
            {
                RoomService.UpdateRent(sharingTypeId, rent);
            }
            _sharingTypes = RoomService.GetSharingTypes();
            BuildRentFields();
            LoadRooms();
            FormFields.ShowSuccess(_rentMessage, $"Rent saved at {DateTime.Now:HH:mm}.");
        }
        catch (ValidationException ex)
        {
            FormFields.ShowError(_rentMessage, ex.Message);
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The rent could not be saved.");
        }
    }

    private void LoadRooms(int? selectRoomId = null)
    {
        try
        {
            _rooms = RoomService.GetRooms();
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
        using var dialog = new RoomEditForm(_sharingTypes);
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

        using var dialog = new RoomEditForm(_sharingTypes, room);
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
