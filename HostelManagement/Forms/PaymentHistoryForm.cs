using HostelManagement.Models;
using HostelManagement.Utilities;

namespace HostelManagement.Forms;

/// <summary>The change history of payments: every correction and deletion, with what changed, when and why.</summary>
public sealed class PaymentHistoryForm : Form
{
    public PaymentHistoryForm(string title, IReadOnlyList<PaymentChange> changes)
    {
        Text = title;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Font;
        Font = UiTheme.BodyFont;
        BackColor = Color.White;
        ClientSize = new Size(980, 460);
        MinimumSize = new Size(700, 300);

        var grid = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false };
        UiTheme.StyleGrid(grid);
        FormFields.AddGridColumn(grid, nameof(PaymentChange.ChangedDate), "Changed on", 12, format: "dd MMM yyyy HH:mm");
        FormFields.AddGridColumn(grid, nameof(PaymentChange.ReceiptNumber), "Receipt no.", 13);
        FormFields.AddGridColumn(grid, nameof(PaymentChange.StudentName), "Student", 13);
        FormFields.AddGridColumn(grid, nameof(PaymentChange.ChangeType), "Change", 7);
        FormFields.AddGridColumn(grid, nameof(PaymentChange.Details), "What changed", 36);
        FormFields.AddGridColumn(grid, nameof(PaymentChange.Reason), "Reason", 19);
        grid.DataSource = changes.ToList();
        grid.CellFormatting += (_, e) =>
        {
            if (e.RowIndex >= 0 && grid.Rows[e.RowIndex].DataBoundItem is PaymentChange { ChangeType: PaymentChangeType.Deleted } &&
                e.CellStyle is not null)
            {
                e.CellStyle.ForeColor = UiTheme.Danger;
            }
        };

        var note = new Label
        {
            Dock = DockStyle.Top,
            Height = 28,
            ForeColor = UiTheme.TextMuted,
            Text = changes.Count == 0 ? "No payment has been corrected or deleted." : $"{changes.Count} change(s), newest first.",
        };
        var closeButton = new Button { Text = "Close", DialogResult = DialogResult.Cancel };
        UiTheme.StyleSecondaryButton(closeButton);
        FlowLayoutPanel bottom = FormFields.CreateButtonRow(closeButton);
        bottom.Dock = DockStyle.Bottom;
        CancelButton = closeButton;

        var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };
        body.Controls.Add(grid);
        body.Controls.Add(note);
        body.Controls.Add(bottom);
        Controls.Add(body);
    }
}
