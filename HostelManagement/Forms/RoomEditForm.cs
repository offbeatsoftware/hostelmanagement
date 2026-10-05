using HostelManagement.Models;
using HostelManagement.Services;
using HostelManagement.Utilities;

namespace HostelManagement.Forms;

/// <summary>Add or edit one room. DialogResult.OK means it was saved.</summary>
public sealed class RoomEditForm : Form
{
    private readonly int _roomId;
    private readonly TextBox _numberBox;
    private readonly TextBox _floorBox;
    private readonly ComboBox _sharingBox;
    private readonly Label _capacityRentLabel;
    private readonly CheckBox _activeBox;
    private readonly TextBox _remarksBox;
    private readonly Label _messageLabel;

    public RoomEditForm(IReadOnlyList<SharingType> sharingTypes, Room? room = null)
    {
        _roomId = room?.RoomId ?? 0;

        Text = room is null ? "Add Room" : $"Edit Room {room.RoomNumber}";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Font;
        Font = UiTheme.BodyFont;
        BackColor = Color.White;
        ClientSize = new Size(560, 380);

        var fields = FormFields.CreateTable(labelWidth: 130, inputWidth: 380);
        _numberBox = FormFields.AddTextBox(fields, "Room number", 20, required: true);
        _floorBox = FormFields.AddTextBox(fields, "Floor", 20);

        _sharingBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = UiTheme.BodyFont,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 4, 0, 4),
            DisplayMember = nameof(SharingType.DisplayName),
            ValueMember = nameof(SharingType.SharingTypeId),
            DataSource = sharingTypes.ToList(),
            TabIndex = fields.Controls.Count,
        };
        FormFields.AddRow(fields, "Sharing type", _sharingBox, required: true);

        _capacityRentLabel = new Label
        {
            AutoSize = true,
            Font = UiTheme.BodyFont,
            ForeColor = UiTheme.TextMuted,
            Margin = new Padding(0, 4, 0, 4),
        };
        FormFields.AddRow(fields, string.Empty, _capacityRentLabel);

        _activeBox = new CheckBox
        {
            Text = "Room is active (students can be allocated)",
            AutoSize = true,
            Font = UiTheme.BodyFont,
            Margin = new Padding(0, 6, 0, 4),
            TabIndex = fields.Controls.Count,
        };
        FormFields.AddRow(fields, "Status", _activeBox);

        _remarksBox = FormFields.AddTextBox(fields, "Remarks", 255, multiline: true);
        fields.Location = new Point(20, 20);

        _numberBox.Text = room?.RoomNumber ?? string.Empty;
        _floorBox.Text = room?.Floor ?? string.Empty;
        _activeBox.Checked = room?.IsActive ?? true;
        _remarksBox.Text = room?.Remarks ?? string.Empty;
        _sharingBox.SelectedIndexChanged += (_, _) => ShowCapacityAndRent();

        // The drop-down is filled by data binding once the form is created, so select the room's type on Load.
        Load += (_, _) =>
        {
            if (room is not null)
            {
                _sharingBox.SelectedValue = room.SharingTypeId;
            }
            ShowCapacityAndRent();
        };

        _messageLabel = FormFields.CreateMessageLabel();
        _messageLabel.Location = new Point(20, 285);
        _messageLabel.MaximumSize = new Size(520, 0);

        var saveButton = new Button { Text = "Save", TabIndex = 1, Location = new Point(310, 330) };
        UiTheme.StylePrimaryButton(saveButton);
        saveButton.Click += (_, _) => Save();

        var cancelButton = new Button { Text = "Cancel", TabIndex = 2, Location = new Point(430, 330) };
        UiTheme.StyleSecondaryButton(cancelButton);
        cancelButton.DialogResult = DialogResult.Cancel;

        AcceptButton = saveButton;
        CancelButton = cancelButton;

        Controls.Add(fields);
        Controls.Add(_messageLabel);
        Controls.Add(saveButton);
        Controls.Add(cancelButton);
    }

    /// <summary>The saved room, available after DialogResult.OK.</summary>
    public Room? SavedRoom { get; private set; }

    private void ShowCapacityAndRent()
    {
        _capacityRentLabel.Text = _sharingBox.SelectedItem is SharingType type
            ? $"Capacity {type.Capacity}, rent {Money.Format(type.Rent)} per student"
            : string.Empty;
    }

    private void Save()
    {
        try
        {
            SavedRoom = RoomService.Save(new Room
            {
                RoomId = _roomId,
                RoomNumber = _numberBox.Text,
                Floor = _floorBox.Text,
                SharingTypeId = _sharingBox.SelectedValue is int id ? id : 0,
                IsActive = _activeBox.Checked,
                Remarks = _remarksBox.Text,
            });
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (ValidationException ex)
        {
            FormFields.ShowError(_messageLabel, ex.Message);
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The room could not be saved.");
        }
    }
}
