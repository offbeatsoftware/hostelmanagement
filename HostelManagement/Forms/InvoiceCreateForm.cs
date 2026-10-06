using HostelManagement.Models;
using HostelManagement.Services;
using HostelManagement.Utilities;

namespace HostelManagement.Forms;

/// <summary>Create an invoice: choose student and billing period, check the calculated lines, then create.</summary>
public sealed class InvoiceCreateForm : Form
{
    private readonly ComboBox _studentBox;
    private readonly ComboBox _periodBox;
    private readonly DateTimePicker _datePicker;
    private readonly DataGridView _itemsGrid;
    private readonly Label _totalLabel;
    private readonly Label _messageLabel;
    private readonly Button _createButton;

    public InvoiceCreateForm(Hostel hostel, IReadOnlyList<Student> students, int? studentId = null)
    {
        Text = "Create Invoice";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Font;
        Font = UiTheme.BodyFont;
        BackColor = Color.White;
        ClientSize = new Size(720, 520);

        TableLayoutPanel fields = FormFields.CreateTable(labelWidth: 130, inputWidth: 400);
        _studentBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 4, 0, 4),
            DisplayMember = nameof(Student.StudentName),
            ValueMember = nameof(Student.StudentId),
            DataSource = students.ToList(),
        };
        FormFields.AddRow(fields, "Student", _studentBox, required: true);

        List<BillingPeriod> periods = InvoiceService.GetBillingPeriods(hostel.BillingFrequency, DateTime.Today);
        _periodBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 260,
            Margin = new Padding(0, 4, 0, 4),
            DisplayMember = nameof(BillingPeriod.Name),
            DataSource = periods,
        };
        FormFields.AddRow(fields, "Billing period", _periodBox, required: true);

        _datePicker = new DateTimePicker
        {
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "dd MMM yyyy",
            Width = 160,
            MaxDate = DateTime.Today,
            Value = DateTime.Today,
            Margin = new Padding(0, 4, 0, 4),
        };
        FormFields.AddRow(fields, "Invoice date", _datePicker, required: true);
        fields.Location = new Point(20, 15);

        _itemsGrid = new DataGridView { Location = new Point(20, 135), Size = new Size(680, 250), AutoGenerateColumns = false };
        UiTheme.StyleGrid(_itemsGrid);
        FormFields.AddGridColumn(_itemsGrid, nameof(InvoiceItem.Description), "Description", 55);
        FormFields.AddGridColumn(_itemsGrid, nameof(InvoiceItem.Quantity), "Qty", 8, alignRight: true);
        FormFields.AddGridColumn(_itemsGrid, nameof(InvoiceItem.Rate), "Rate", 17, format: "C2", alignRight: true);
        FormFields.AddGridColumn(_itemsGrid, nameof(InvoiceItem.Amount), "Amount", 20, format: "C2", alignRight: true);

        _totalLabel = new Label
        {
            AutoSize = false,
            Size = new Size(680, 26),
            Location = new Point(20, 392),
            Font = UiTheme.BodyBoldFont,
            TextAlign = ContentAlignment.MiddleRight,
        };

        _messageLabel = FormFields.CreateMessageLabel();
        _messageLabel.Location = new Point(20, 425);
        _messageLabel.MaximumSize = new Size(680, 0);

        _createButton = new Button { Text = "Create", Location = new Point(470, 470) };
        UiTheme.StylePrimaryButton(_createButton);
        _createButton.Click += (_, _) => CreateInvoice();

        var cancelButton = new Button { Text = "Cancel", Location = new Point(590, 470) };
        UiTheme.StyleSecondaryButton(cancelButton);
        cancelButton.DialogResult = DialogResult.Cancel;

        AcceptButton = _createButton;
        CancelButton = cancelButton;

        Controls.AddRange([fields, _itemsGrid, _totalLabel, _messageLabel, _createButton, cancelButton]);

        Load += (_, _) =>
        {
            // The lists are filled by data binding once the form exists.
            if (studentId is not null)
            {
                _studentBox.SelectedValue = studentId.Value;
            }
            BillingPeriod current = BillingPeriods.For(DateTime.Today, hostel.BillingFrequency);
            _periodBox.SelectedIndex = Math.Max(periods.IndexOf(current), 0);

            _studentBox.SelectedIndexChanged += (_, _) => ShowPreview();
            _periodBox.SelectedIndexChanged += (_, _) => ShowPreview();
            _datePicker.ValueChanged += (_, _) => ShowPreview();
            ShowPreview();
        };
    }

    public Invoice? CreatedInvoice { get; private set; }

    private int StudentId => _studentBox.SelectedValue is int id ? id : 0;

    /// <summary>Shows the calculated lines, or why this invoice cannot be created.</summary>
    private void ShowPreview()
    {
        _messageLabel.Text = string.Empty;
        _itemsGrid.DataSource = null;
        _totalLabel.Text = string.Empty;
        _createButton.Enabled = false;

        if (StudentId == 0 || _periodBox.SelectedItem is not BillingPeriod period)
        {
            return;
        }

        try
        {
            Invoice preview = InvoiceService.Preview(StudentId, period, _datePicker.Value.Date);
            _itemsGrid.DataSource = preview.Items;
            _totalLabel.Text = $"Total {Money.Format(preview.TotalAmount)}";
            _createButton.Enabled = true;
        }
        catch (ValidationException ex)
        {
            FormFields.ShowError(_messageLabel, ex.Message);
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The invoice could not be calculated.");
        }
    }

    private void CreateInvoice()
    {
        if (_periodBox.SelectedItem is not BillingPeriod period)
        {
            return;
        }

        try
        {
            CreatedInvoice = InvoiceService.Create(StudentId, period, _datePicker.Value.Date);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (ValidationException ex)
        {
            FormFields.ShowError(_messageLabel, ex.Message);
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The invoice could not be created.");
        }
    }
}
