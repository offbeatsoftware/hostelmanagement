using HostelManagement.Models;
using HostelManagement.Services;
using HostelManagement.Utilities;

namespace HostelManagement.Forms.Views;

/// <summary>Services of the selected hostel: included in rent or charged extra per month.</summary>
public sealed class ServicesView : UserControl
{
    private readonly int _hostelId;
    private readonly DataGridView _grid;
    private readonly Button _editButton;
    private readonly Button _deleteButton;

    public ServicesView(Hostel hostel)
    {
        _hostelId = hostel.HostelId;
        Dock = DockStyle.Fill;
        BackColor = UiTheme.ContentBackground;

        var addButton = new Button { Text = "Add" };
        UiTheme.StylePrimaryButton(addButton);
        addButton.Click += (_, _) => AddService();

        _editButton = new Button { Text = "Edit" };
        UiTheme.StyleSecondaryButton(_editButton);
        _editButton.Click += (_, _) => EditSelectedService();

        _deleteButton = new Button { Text = "Delete" };
        UiTheme.StyleDangerButton(_deleteButton);
        _deleteButton.Click += (_, _) => DeleteSelectedService();

        var hint = FormFields.CreateMessageLabel();
        hint.ForeColor = UiTheme.TextMuted;
        hint.Text = "Students choose extra services on the Services tab of the student form.";

        FlowLayoutPanel toolbar = FormFields.CreateButtonRow(addButton, _editButton, _deleteButton, hint);
        toolbar.Dock = DockStyle.Top;

        _grid = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false };
        UiTheme.StyleGrid(_grid);
        FormFields.AddGridColumn(_grid, nameof(ServiceItem.ServiceName), "Service", 25);
        FormFields.AddGridColumn(_grid, nameof(ServiceItem.ChargeText), "Charge", 20);
        FormFields.AddGridColumn(_grid, nameof(ServiceItem.MonthlyRate), "Monthly rate", 15, format: "C2", alignRight: true);
        FormFields.AddGridColumn(_grid, nameof(ServiceItem.StudentCount), "Students using", 12, alignRight: true);
        FormFields.AddGridColumn(_grid, nameof(ServiceItem.Status), "Status", 10);
        _grid.CellFormatting += (_, e) =>
        {
            if (e.RowIndex >= 0 && _grid.Rows[e.RowIndex].DataBoundItem is ServiceItem service &&
                _grid.Columns[e.ColumnIndex].Name == nameof(ServiceItem.MonthlyRate) && service.IsIncludedInRent)
            {
                e.Value = "-";
                e.FormattingApplied = true;
            }
        };
        _grid.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0)
            {
                EditSelectedService();
            }
        };
        _grid.SelectionChanged += (_, _) => UpdateButtons();

        var body = new Panel { Dock = DockStyle.Fill };
        body.Controls.Add(_grid);
        body.Controls.Add(toolbar);
        Panel card = FormFields.CreateCard($"Services of {hostel.HostelName}", body);
        card.Dock = DockStyle.Fill;
        Controls.Add(card);

        Load += (_, _) => LoadServices();
    }

    private ServiceItem? SelectedService => _grid.CurrentRow?.DataBoundItem as ServiceItem;

    private void LoadServices(int? selectServiceId = null)
    {
        try
        {
            List<ServiceItem> services = ServiceItemService.GetServices(_hostelId);
            _grid.DataSource = services;
            if (selectServiceId is not null)
            {
                int index = services.FindIndex(s => s.ServiceId == selectServiceId);
                if (index >= 0)
                {
                    _grid.CurrentCell = _grid.Rows[index].Cells[0];
                }
            }
            UpdateButtons();
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The services could not be loaded.");
        }
    }

    private void UpdateButtons()
    {
        bool hasSelection = SelectedService is not null;
        _editButton.Enabled = hasSelection;
        _deleteButton.Enabled = hasSelection;
    }

    private void AddService()
    {
        using var dialog = new ServiceEditForm(_hostelId);
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            LoadServices(dialog.SavedService?.ServiceId);
        }
    }

    private void EditSelectedService()
    {
        if (SelectedService is not ServiceItem service)
        {
            return;
        }

        using var dialog = new ServiceEditForm(_hostelId, service);
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            LoadServices(service.ServiceId);
        }
    }

    private void DeleteSelectedService()
    {
        if (SelectedService is not ServiceItem service || !Dialogs.Confirm($"Delete the service \"{service.ServiceName}\"?"))
        {
            return;
        }

        try
        {
            ServiceItemService.Delete(service.ServiceId);
            LoadServices();
        }
        catch (ValidationException ex)
        {
            Dialogs.Warning(ex.Message);
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The service could not be deleted.");
        }
    }
}
