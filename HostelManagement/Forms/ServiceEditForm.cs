using HostelManagement.Models;
using HostelManagement.Services;
using HostelManagement.Utilities;

namespace HostelManagement.Forms;

/// <summary>Add or edit a hostel service. DialogResult.OK means it was saved.</summary>
public sealed class ServiceEditForm : Form
{
    private readonly int _hostelId;
    private readonly int _serviceId;
    private readonly TextBox _nameBox;
    private readonly RadioButton _includedButton;
    private readonly RadioButton _extraButton;
    private readonly TextBox _rateBox;
    private readonly CheckBox _activeBox;
    private readonly Label _messageLabel;

    public ServiceEditForm(int hostelId, ServiceItem? service = null)
    {
        _hostelId = hostelId;
        _serviceId = service?.ServiceId ?? 0;

        Text = service is null ? "Add Service" : $"Edit Service: {service.ServiceName}";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Font;
        Font = UiTheme.BodyFont;
        BackColor = Color.White;
        ClientSize = new Size(560, 320);

        TableLayoutPanel fields = FormFields.CreateTable(labelWidth: 130, inputWidth: 380);
        _nameBox = FormFields.AddTextBox(fields, "Service name", 100, required: true);

        _includedButton = new RadioButton { Text = "Included in the room rent (no extra charge)", AutoSize = true, Margin = new Padding(0, 6, 0, 0) };
        _extraButton = new RadioButton { Text = "Charged extra per month to students who use it", AutoSize = true, Margin = new Padding(0, 4, 0, 4) };
        var chargePanel = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        chargePanel.Controls.Add(_includedButton);
        chargePanel.Controls.Add(_extraButton);
        FormFields.AddRow(fields, "Charge", chargePanel, required: true);

        _rateBox = FormFields.AddTextBox(fields, "Monthly rate", 15);
        _rateBox.TextAlign = HorizontalAlignment.Right;
        _rateBox.Dock = DockStyle.None;
        _rateBox.Width = 160;

        _activeBox = new CheckBox { Text = "Active", AutoSize = true, Margin = new Padding(0, 6, 0, 4) };
        FormFields.AddRow(fields, "Status", _activeBox);
        fields.Location = new Point(20, 20);

        _nameBox.Text = service?.ServiceName ?? string.Empty;
        _includedButton.Checked = service?.IsIncludedInRent ?? false;
        _extraButton.Checked = !(service?.IsIncludedInRent ?? false);
        _rateBox.Text = service is { IsIncludedInRent: false } ? service.MonthlyRate.ToString("N2", Money.Culture) : string.Empty;
        _activeBox.Checked = service?.IsActive ?? true;
        _includedButton.CheckedChanged += (_, _) => _rateBox.Enabled = _extraButton.Checked;
        _rateBox.Enabled = _extraButton.Checked;

        _messageLabel = FormFields.CreateMessageLabel();
        _messageLabel.Location = new Point(20, 225);
        _messageLabel.MaximumSize = new Size(520, 0);

        var saveButton = new Button { Text = "Save", Location = new Point(310, 270) };
        UiTheme.StylePrimaryButton(saveButton);
        saveButton.Click += (_, _) => Save();

        var cancelButton = new Button { Text = "Cancel", Location = new Point(430, 270) };
        UiTheme.StyleSecondaryButton(cancelButton);
        cancelButton.DialogResult = DialogResult.Cancel;

        AcceptButton = saveButton;
        CancelButton = cancelButton;

        Controls.Add(fields);
        Controls.Add(_messageLabel);
        Controls.Add(saveButton);
        Controls.Add(cancelButton);
    }

    public ServiceItem? SavedService { get; private set; }

    private void Save()
    {
        decimal rate = 0m;
        if (_extraButton.Checked && !Money.TryParse(_rateBox.Text, out rate))
        {
            FormFields.ShowError(_messageLabel, "Please enter a valid monthly rate, for example 1500.");
            return;
        }

        try
        {
            SavedService = ServiceItemService.Save(new ServiceItem
            {
                ServiceId = _serviceId,
                HostelId = _hostelId,
                ServiceName = _nameBox.Text,
                IsIncludedInRent = _includedButton.Checked,
                MonthlyRate = rate,
                IsActive = _activeBox.Checked,
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
            ErrorHandler.Handle(ex, "The service could not be saved.");
        }
    }
}
