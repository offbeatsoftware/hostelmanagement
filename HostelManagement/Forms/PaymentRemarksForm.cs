using HostelManagement.Models;
using HostelManagement.Utilities;

namespace HostelManagement.Forms;

/// <summary>Shows the full remarks of a payment, with the payment details for reference (read only; Edit changes them).</summary>
public sealed class PaymentRemarksForm : Form
{
    public PaymentRemarksForm(Payment payment)
    {
        Text = $"Remarks: {payment.ReceiptNumber}";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Font;
        Font = UiTheme.BodyFont;
        BackColor = Color.White;
        ClientSize = new Size(520, 300);

        var details = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(480, 0),
            Location = new Point(20, 18),
            ForeColor = UiTheme.TextMuted,
            Text = $"{Money.Format(payment.Amount)} paid by {payment.StudentName} on {payment.PaymentDate:dd MMM yyyy} " +
                   $"({payment.PaymentMethod}, receipt {payment.ReceiptNumber}, invoice {payment.InvoiceNumber})",
        };
        var remarksBox = new TextBox
        {
            Location = new Point(20, 70),
            Size = new Size(480, 160),
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            BackColor = Color.White,
            Text = payment.Remarks.Length > 0 ? payment.Remarks : "No remarks for this payment.",
        };
        var note = new Label
        {
            AutoSize = true,
            Location = new Point(20, 250),
            ForeColor = UiTheme.TextMuted,
            Text = "To change the remarks, use Edit.",
        };
        var closeButton = new Button { Text = "Close", Location = new Point(400, 250), DialogResult = DialogResult.Cancel };
        UiTheme.StyleSecondaryButton(closeButton);

        CancelButton = closeButton;
        AcceptButton = closeButton;
        Controls.AddRange([details, remarksBox, note, closeButton]);
        Shown += (_, _) => closeButton.Focus();
    }
}
