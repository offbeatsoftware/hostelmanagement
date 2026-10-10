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
/// Check-in (choose student, room and bed, and enter the student's room rent and transport for the academic
/// year), transfer (choose the new room) or check-out of one student. Rooms offered are the hostel's active
/// rooms for the student's gender with a free bed.
/// DialogResult.OK means it was saved.
/// </summary>
public sealed class AllocationForm : Form
{
    private readonly AllocationAction _action;
    private readonly int _hostelId;
    private readonly RoomAllocation? _current;
    private readonly ComboBox? _studentBox;
    private readonly ComboBox? _roomBox;
    private readonly ComboBox? _bedBox;
    private readonly DateTimePicker _datePicker;
    private readonly TextBox _remarksBox;
    private readonly TextBox? _rentBox;
    private readonly TextBox? _transportBox;
    private readonly Label? _feeLabel;
    private readonly Label _messageLabel;
    private Invoice? _existingFee;

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
        ClientSize = new Size(580, action == AllocationAction.CheckIn ? 515 : 365);

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

            _bedBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = UiTheme.BodyFont,
                Width = 120,
                Margin = new Padding(0, 4, 0, 4),
            };
            FormFields.AddRow(fields, "Bed", _bedBox, required: true);
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

        if (action == AllocationAction.CheckIn)
        {
            // The fee agreed with this student for the academic year (client decision, version 1.2).
            _rentBox = FormFields.AddTextBox(fields, "Room rent / year", 15, required: true);
            _transportBox = FormFields.AddTextBox(fields, "Transport / year", 15);
            _rentBox.Width = _transportBox.Width = 160;
            _rentBox.Dock = _transportBox.Dock = DockStyle.None;
            _rentBox.TextAlign = _transportBox.TextAlign = HorizontalAlignment.Right;
            _transportBox.PlaceholderText = "0 if no transport";
            _feeLabel = CreateInfoLabel(string.Empty);
            FormFields.AddRow(fields, "Total fee", _feeLabel);
            _rentBox.TextChanged += (_, _) => ShowFeeTotal();
            _transportBox.TextChanged += (_, _) => ShowFeeTotal();
            _datePicker.ValueChanged += (_, _) => ShowExistingFee();
        }
        fields.Location = new Point(20, 20);
        int bottom = action == AllocationAction.CheckIn ? 105 : 0;

        var note = new Label
        {
            AutoSize = true,
            ForeColor = UiTheme.TextMuted,
            Location = new Point(20, 250 + bottom),
            MaximumSize = new Size(540, 0),
            Text = action switch
            {
                AllocationAction.Transfer => "The student leaves the current room and enters the new room on this date.",
                AllocationAction.CheckOut => "The student's status will be set to Left.",
                _ => "Only active rooms for the student's gender with a free bed are listed. The fee is for the academic " +
                     "year of the check-in date (July to June); the student can pay it in any number of payments.",
            },
        };

        _messageLabel = FormFields.CreateMessageLabel();
        _messageLabel.Location = new Point(20, 280 + bottom + (action == AllocationAction.CheckIn ? 18 : 0));
        _messageLabel.MaximumSize = new Size(540, 0);

        var saveButton = new Button
        {
            Text = action switch
            {
                AllocationAction.CheckIn => "Check-in",
                AllocationAction.Transfer => "Transfer",
                _ => "Check-out",
            },
            Location = new Point(330, 315 + bottom + (action == AllocationAction.CheckIn ? 46 : 0)),
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

        var cancelButton = new Button { Text = "Cancel", Location = new Point(450, 315 + bottom + (action == AllocationAction.CheckIn ? 46 : 0)) };
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
                _studentBox.SelectedIndexChanged += (_, _) =>
                {
                    LoadRooms();
                    ShowExistingFee();
                };
            }
            if (_roomBox is not null)
            {
                _roomBox.SelectedIndexChanged += (_, _) => LoadBeds();
            }
            LoadRooms();
            ShowExistingFee();
        };
    }

    /// <summary>A student who already has a fee for the year (for example after leaving and coming back) keeps it.</summary>
    private void ShowExistingFee()
    {
        if (_rentBox is null || _transportBox is null)
        {
            return;
        }
        try
        {
            _existingFee = SelectedStudentId == 0
                ? null
                : InvoiceService.GetForYear(SelectedStudentId, AcademicYear.Of(_datePicker.Value));
            _rentBox.Enabled = _transportBox.Enabled = _existingFee is null;
            if (_existingFee is not null)
            {
                _rentBox.Text = _existingFee.RoomRent.ToString("N2", Money.Culture);
                _transportBox.Text = _existingFee.TransportAmount.ToString("N2", Money.Culture);
            }
            ShowFeeTotal();
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The student's fee could not be loaded.");
        }
    }

    private void ShowFeeTotal()
    {
        if (_feeLabel is null)
        {
            return;
        }
        if (_existingFee is not null)
        {
            _feeLabel.Text = $"{Money.Format(_existingFee.TotalAmount)} (already set for {_existingFee.YearText}, " +
                             $"invoice {_existingFee.InvoiceNumber})";
            return;
        }
        bool rentOk = Money.TryParse(_rentBox!.Text, out decimal rent);
        bool transportOk = Money.TryParse(_transportBox!.Text, out decimal transport) || _transportBox.Text.Trim().Length == 0;
        _feeLabel.Text = rentOk && transportOk
            ? $"{Money.Format(rent + (transportOk ? transport : 0))} for {AcademicYear.Label(AcademicYear.Of(_datePicker.Value))}"
            : string.Empty;
    }

    /// <summary>The fee typed in, or null (with a message) when it is not valid.</summary>
    private YearFee? ReadFee()
    {
        if (!Money.TryParse(_rentBox!.Text, out decimal rent) || rent <= 0)
        {
            FormFields.ShowError(_messageLabel, "Please enter the room rent for the year agreed with the student.");
            _rentBox.Focus();
            return null;
        }
        decimal transport = 0;
        if (_transportBox!.Text.Trim().Length > 0 && !Money.TryParse(_transportBox.Text, out transport))
        {
            FormFields.ShowError(_messageLabel, "Please enter the transport amount for the year, or leave it empty.");
            _transportBox.Focus();
            return null;
        }
        return new YearFee(rent, transport);
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
            LoadBeds();

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

    /// <summary>Lists the free beds of the chosen room; the lowest is chosen.</summary>
    private void LoadBeds()
    {
        if (_bedBox is null)
        {
            return;
        }

        try
        {
            _bedBox.Items.Clear();
            if (_roomBox?.SelectedValue is int roomId && roomId > 0)
            {
                foreach (int bed in AllocationService.GetFreeBeds(roomId))
                {
                    _bedBox.Items.Add(bed);
                }
            }
            if (_bedBox.Items.Count > 0)
            {
                _bedBox.SelectedIndex = 0;
            }
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The beds could not be loaded.");
        }
    }

    /// <summary>The allocation made by a check-in, for offering the agreement afterwards.</summary>
    public RoomAllocation? SavedAllocation { get; private set; }

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
                    YearFee? fee = null;
                    if (_existingFee is null)
                    {
                        fee = ReadFee();
                        if (fee is null)
                        {
                            return;
                        }
                    }
                    SavedAllocation = AllocationService.CheckIn(studentId, roomId, date, _remarksBox.Text, _bedBox?.SelectedItem as int?, fee);
                    break;

                case AllocationAction.Transfer:
                    string newRoom = (_roomBox!.SelectedItem as Room)?.RoomNumber ?? string.Empty;
                    if (!Dialogs.Confirm($"Move {_current!.StudentName} from room {_current.RoomNumber} to room {newRoom} on {date:dd MMM yyyy}?"))
                    {
                        return;
                    }
                    SavedAllocation = AllocationService.Transfer(studentId, roomId, date, _remarksBox.Text, _bedBox?.SelectedItem as int?);
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
