using HostelManagement.Models;
using HostelManagement.Services;
using HostelManagement.Utilities;

namespace HostelManagement.Forms;

public enum AllocationAction
{
    CheckIn,
    Transfer,
    CheckOut,
}

/// <summary>
/// Check-in (choose student and room), transfer (choose the new room) or check-out of one student.
/// Rooms offered are the hostel's active rooms for the student's gender with a free bed.
/// DialogResult.OK means it was saved.
/// </summary>
public sealed class AllocationForm : Form
{
    private readonly AllocationAction _action;
    private readonly int _hostelId;
    private readonly RoomAllocation? _current;
    private readonly ComboBox? _studentBox;
    private readonly ComboBox? _roomBox;
    private readonly DateTimePicker _datePicker;
    private readonly TextBox _remarksBox;
    private readonly Label _messageLabel;

    /// <param name="students">Students without a room (check-in only).</param>
    /// <param name="current">The student's current allocation (transfer and check-out).</param>
    public AllocationForm(AllocationAction action, int hostelId, IReadOnlyList<Student>? students = null,
        RoomAllocation? current = null)
    {
        _action = action;
        _hostelId = hostelId;
        _current = current;

        Text = action switch
        {
            AllocationAction.CheckIn => "Check-in",
            AllocationAction.Transfer => $"Transfer {current?.StudentName}",
            _ => $"Check-out {current?.StudentName}",
        };
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Font;
        Font = UiTheme.BodyFont;
        BackColor = Color.White;
        ClientSize = new Size(580, 330);

        TableLayoutPanel fields = FormFields.CreateTable(labelWidth: 130, inputWidth: 400);

        if (action == AllocationAction.CheckIn)
        {
            _studentBox = CreateCombo(nameof(Student.StudentName), nameof(Student.StudentId));
            _studentBox.DataSource = (students ?? []).ToList();
            FormFields.AddRow(fields, "Student", _studentBox, required: true);
        }
        else
        {
            FormFields.AddRow(fields, "Student", CreateInfoLabel(current?.StudentName ?? string.Empty));
            FormFields.AddRow(fields, "Current room", CreateInfoLabel(
                $"{current?.RoomNumber} (since {current?.CheckInDate:dd MMM yyyy})"));
        }

        if (action != AllocationAction.CheckOut)
        {
            _roomBox = CreateCombo(nameof(Room.DisplayName), nameof(Room.RoomId));
            FormFields.AddRow(fields, action == AllocationAction.Transfer ? "New room" : "Room", _roomBox, required: true);
        }

        _datePicker = new DateTimePicker
        {
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "dd MMM yyyy",
            Font = UiTheme.BodyFont,
            Width = 200,
            MaxDate = DateTime.Today,
            Value = DateTime.Today,
            Margin = new Padding(0, 4, 0, 4),
        };
        string dateCaption = action switch
        {
            AllocationAction.CheckIn => "Check-in date",
            AllocationAction.Transfer => "Transfer date",
            _ => "Check-out date",
        };
        FormFields.AddRow(fields, dateCaption, _datePicker, required: true);
        _remarksBox = FormFields.AddTextBox(fields, "Remarks", 255, multiline: true);
        fields.Location = new Point(20, 20);

        var note = new Label
        {
            AutoSize = true,
            ForeColor = UiTheme.TextMuted,
            Location = new Point(20, 215),
            MaximumSize = new Size(540, 0),
            Text = action switch
            {
                AllocationAction.Transfer => "The student leaves the current room and enters the new room on this date.",
                AllocationAction.CheckOut => "The student's status will be set to Left.",
                _ => "Only active rooms for the student's gender with a free bed are listed.",
            },
        };

        _messageLabel = FormFields.CreateMessageLabel();
        _messageLabel.Location = new Point(20, 245);
        _messageLabel.MaximumSize = new Size(540, 0);

        var saveButton = new Button
        {
            Text = action switch
            {
                AllocationAction.CheckIn => "Check-in",
                AllocationAction.Transfer => "Transfer",
                _ => "Check-out",
            },
            Location = new Point(330, 280),
        };
        if (action == AllocationAction.CheckOut)
        {
            UiTheme.StyleDangerButton(saveButton);
        }
        else
        {
            UiTheme.StylePrimaryButton(saveButton);
        }
        saveButton.Click += (_, _) => Save();

        var cancelButton = new Button { Text = "Cancel", Location = new Point(450, 280) };
        UiTheme.StyleSecondaryButton(cancelButton);
        cancelButton.DialogResult = DialogResult.Cancel;

        AcceptButton = saveButton;
        CancelButton = cancelButton;

        Controls.Add(fields);
        Controls.Add(note);
        Controls.Add(_messageLabel);
        Controls.Add(saveButton);
        Controls.Add(cancelButton);

        Load += (_, _) =>
        {
            // The student list is filled by data binding once the form exists.
            if (_studentBox is not null)
            {
                _studentBox.SelectedIndexChanged += (_, _) => LoadRooms();
            }
            LoadRooms();
        };
    }

    private static ComboBox CreateCombo(string displayMember, string valueMember) => new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Font = UiTheme.BodyFont,
        Dock = DockStyle.Fill,
        Margin = new Padding(0, 4, 0, 4),
        DisplayMember = displayMember,
        ValueMember = valueMember,
    };

    private static Label CreateInfoLabel(string text) => new()
    {
        AutoSize = true,
        Font = UiTheme.BodyBoldFont,
        Margin = new Padding(0, 8, 0, 4),
        Text = text,
    };

    private int SelectedStudentId =>
        _studentBox is null ? _current?.StudentId ?? 0 : _studentBox.SelectedValue is int id ? id : 0;

    /// <summary>Lists the rooms the selected student can move into.</summary>
    private void LoadRooms()
    {
        if (_roomBox is null)
        {
            return;
        }

        try
        {
            Student? student = StudentService.GetStudent(SelectedStudentId);
            List<Room> rooms = student is null || !RoomGender.All.Contains(student.Gender)
                ? []
                : AllocationService.GetRoomsFor(_hostelId, student.Gender, _current?.RoomId ?? 0);
            _roomBox.DataSource = rooms;

            _messageLabel.Text = string.Empty;
            if (student is not null && !RoomGender.All.Contains(student.Gender))
            {
                FormFields.ShowError(_messageLabel, $"Set {student.StudentName}'s gender on the Students screen first.");
            }
            else if (student is not null && rooms.Count == 0)
            {
                FormFields.ShowError(_messageLabel,
                    $"There is no free bed in a room for {RoomGender.DisplayName(student.Gender).ToLowerInvariant()}.");
            }
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The rooms could not be loaded.");
        }
    }

    private void Save()
    {
        try
        {
            int studentId = SelectedStudentId;
            if (studentId == 0)
            {
                FormFields.ShowError(_messageLabel, "Please select the student.");
                return;
            }
            int roomId = _roomBox?.SelectedValue is int id ? id : 0;
            if (_roomBox is not null && roomId == 0)
            {
                FormFields.ShowError(_messageLabel, "Please select the room.");
                return;
            }

            DateTime date = _datePicker.Value.Date;
            switch (_action)
            {
                case AllocationAction.CheckIn:
                    AllocationService.CheckIn(studentId, roomId, date, _remarksBox.Text);
                    break;

                case AllocationAction.Transfer:
                    string newRoom = (_roomBox!.SelectedItem as Room)?.RoomNumber ?? string.Empty;
                    if (!Dialogs.Confirm($"Move {_current!.StudentName} from room {_current.RoomNumber} to room {newRoom} on {date:dd MMM yyyy}?"))
                    {
                        return;
                    }
                    AllocationService.Transfer(studentId, roomId, date, _remarksBox.Text);
                    break;

                default:
                    if (!Dialogs.Confirm($"Check out {_current!.StudentName} from room {_current.RoomNumber} on {date:dd MMM yyyy}? " +
                                         "The student's status will be set to Left."))
                    {
                        return;
                    }
                    AllocationService.CheckOut(studentId, date, _remarksBox.Text);
                    break;
            }

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (ValidationException ex)
        {
            FormFields.ShowError(_messageLabel, ex.Message);
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The room allocation could not be saved.");
        }
    }
}
