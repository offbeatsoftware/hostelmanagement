using HostelManagement.Models;
using HostelManagement.Services;
using HostelManagement.Utilities;

namespace HostelManagement.Forms;

/// <summary>Confirms deleting a payment entered by mistake and asks for the reason, which is kept in the change history.</summary>
public sealed class PaymentDeleteForm : Form
{
    private readonly Payment _payment;
    private readonly TextBox _reasonBox;
    private readonly Label _messageLabel;

    public PaymentDeleteForm(Payment payment)
    {
        _payment = payment;

        Text = $"Delete Payment {payment.ReceiptNumber}";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Font;
        Font = UiTheme.BodyFont;
        BackColor = Color.White;
        ClientSize = new Size(560, 250);

        var question = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(520, 0),
            Location = new Point(20, 20),
            Text = $"Delete {Money.Format(payment.Amount)} paid by {payment.StudentName} on {payment.PaymentDate:dd MMM yyyy} " +
                   $"({payment.PaymentMethod}, receipt {payment.ReceiptNumber})?\n\nThe amount becomes pending again on invoice " +
                   $"{payment.InvoiceNumber}. The deletion is kept in the change history.",
        };
        var reasonLabel = new Label { AutoSize = true, Location = new Point(20, 120), Text = "Reason" };
        _reasonBox = new TextBox
        {
            Location = new Point(90, 116),
            Width = 450,
            MaxLength = 255,
            PlaceholderText = "For example: entered twice by mistake",
        };
        _messageLabel = FormFields.CreateMessageLabel();
        _messageLabel.Location = new Point(20, 155);
        _messageLabel.MaximumSize = new Size(520, 0);

        var deleteButton = new Button { Text = "Delete", Location = new Point(310, 200) };
        UiTheme.StyleDangerButton(deleteButton);
        deleteButton.Click += (_, _) => Delete();
        var cancelButton = new Button { Text = "Cancel", Location = new Point(430, 200), DialogResult = DialogResult.Cancel };
        UiTheme.StyleSecondaryButton(cancelButton);

        CancelButton = cancelButton;
        Controls.AddRange([question, reasonLabel, _reasonBox, _messageLabel, deleteButton, cancelButton]);
    }

    private void Delete()
    {
        try
        {
            PaymentService.Delete(_payment.PaymentId, _reasonBox.Text);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (ValidationException ex)
        {
            FormFields.ShowError(_messageLabel, ex.Message);
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The payment could not be deleted.");
        }
    }
}
