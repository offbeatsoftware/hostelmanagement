using HostelManagement.Models;
using HostelManagement.Services;
using HostelManagement.Utilities;

namespace HostelManagement.Forms;

/// <summary>All refunds of the hostel, newest first, with the refund receipt (view/print, email) and Delete for a mistake.</summary>
public sealed class RefundsForm : Form
{
    private readonly int _hostelId;
    private readonly DataGridView _grid;
    private readonly Label _summaryLabel;

    public RefundsForm(int hostelId)
    {
        _hostelId = hostelId;
        Text = "Refunds";
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Font;
        Font = UiTheme.BodyFont;
        BackColor = Color.White;
        ClientSize = new Size(960, 520);
        MinimumSize = new Size(760, 400);

        _grid = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false };
        UiTheme.StyleGrid(_grid);
        FormFields.AddGridColumn(_grid, nameof(Refund.RefundNumber), "Refund no.", 15);
        FormFields.AddGridColumn(_grid, nameof(Refund.RefundDate), "Date", 9, format: "dd MMM yyyy");
        FormFields.AddGridColumn(_grid, nameof(Refund.StudentName), "Student", 14);
        FormFields.AddGridColumn(_grid, nameof(Refund.InvoiceNumber), "Invoice no.", 13);
        FormFields.AddGridColumn(_grid, nameof(Refund.RefundMethod), "Paid back by", 9);
        FormFields.AddGridColumn(_grid, nameof(Refund.Reference), "Reference", 10);
        FormFields.AddGridColumn(_grid, nameof(Refund.Amount), "Amount", 9, format: "C2", alignRight: true);
        FormFields.AddGridColumn(_grid, nameof(Refund.Reason), "Reason", 21);

        var viewButton = new Button { Text = "View / Print" };
        UiTheme.StylePrimaryButton(viewButton);
        viewButton.Width = 120;
        viewButton.Click += (_, _) =>
        {
            if (Selected is Refund refund)
            {
                RefundActions.ViewReceipt(refund.RefundId);
            }
        };
        var emailButton = new Button { Text = "Email Receipt" };
        UiTheme.StyleSecondaryButton(emailButton);
        emailButton.Width = 130;
        emailButton.Click += async (_, _) =>
        {
            if (Selected is Refund refund)
            {
                await RefundActions.EmailReceiptAsync(this, refund.RefundId);
            }
        };
        var deleteButton = new Button { Text = "Delete" };
        UiTheme.StyleDangerButton(deleteButton);
        deleteButton.Click += (_, _) =>
        {
            if (Selected is Refund refund && RefundActions.Delete(this, refund))
            {
                LoadRefunds();
            }
        };
        var closeButton = new Button { Text = "Close", DialogResult = DialogResult.Cancel };
        UiTheme.StyleSecondaryButton(closeButton);
        CancelButton = closeButton;
        _summaryLabel = FormFields.CreateMessageLabel();
        _summaryLabel.ForeColor = UiTheme.TextMuted;
        FlowLayoutPanel toolbar = FormFields.CreateButtonRow(viewButton, emailButton, deleteButton, closeButton, _summaryLabel);
        toolbar.Dock = DockStyle.Top;

        var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };
        body.Controls.Add(_grid);
        body.Controls.Add(toolbar);
        Controls.Add(body);

        Load += (_, _) => LoadRefunds();
    }

    private Refund? Selected => _grid.CurrentRow?.DataBoundItem as Refund;

    private void LoadRefunds()
    {
        try
        {
            List<Refund> refunds = RefundService.GetRefunds(_hostelId);
            _grid.DataSource = refunds;
            _summaryLabel.Text = $"{refunds.Count} refunds, {Money.Format(refunds.Sum(r => r.Amount))} paid back";
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The refunds could not be loaded.");
        }
    }
}
