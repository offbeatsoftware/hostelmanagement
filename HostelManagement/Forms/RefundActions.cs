using System.Diagnostics;
using HostelManagement.Models;
using HostelManagement.Reports;
using HostelManagement.Services;
using HostelManagement.Utilities;

namespace HostelManagement.Forms;

/// <summary>Refund a student, and view, email or delete a refund (used by the Students and Payments screens).</summary>
internal static class RefundActions
{
    /// <summary>Opens the Refund window for the student; afterwards offers the refund receipt. Returns the refund, or null.</summary>
    public static async Task<Refund?> RefundAsync(Control screen, int studentId, int? invoiceId = null)
    {
        try
        {
            Student student = StudentService.GetStudent(studentId) ?? throw new ValidationException("This student no longer exists.");
            List<Invoice> invoices = RefundService.GetRefundableInvoices(studentId);
            if (invoices.Count == 0)
            {
                Dialogs.Warning($"Nothing has been paid by {student.StudentName} (after refunds), so there is nothing to refund.");
                return null;
            }

            using var dialog = new RefundForm(student, invoices, invoiceId);
            if (dialog.ShowDialog(screen.FindForm()) != DialogResult.OK || dialog.SavedRefund is not Refund refund)
            {
                return null;
            }

            if (Dialogs.Confirm($"Refund {refund.RefundNumber} of {Money.Format(refund.Amount)} was saved.\n\nOpen the refund receipt to print it?"))
            {
                ViewReceipt(refund.RefundId);
            }
            if (Dialogs.Confirm("Email the refund receipt to the parent?"))
            {
                await EmailReceiptAsync(screen, refund.RefundId);
            }
            return refund;
        }
        catch (ValidationException ex)
        {
            Dialogs.Warning(ex.Message);
            return null;
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The refund could not be made.");
            return null;
        }
    }

    public static void ViewReceipt(int refundId)
    {
        try
        {
            string path = RefundReceiptPdfWriter.SaveToReceiptsFolder(RefundService.GetPrintData(refundId));
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The refund receipt could not be opened. Check that a PDF viewer is installed.");
        }
    }

    public static async Task EmailReceiptAsync(Control screen, int refundId)
    {
        try
        {
            OutgoingEmail email = EmailService.PrepareRefundReceipt(refundId);
            await EmailSending.SendAsync(screen, [email]);
        }
        catch (ValidationException ex)
        {
            Dialogs.Warning(ex.Message);
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The refund receipt could not be emailed.");
        }
    }

    /// <summary>Deletes a refund entered by mistake, after asking for the reason. Returns true when deleted.</summary>
    public static bool Delete(IWin32Window owner, Refund refund)
    {
        using var ask = new ReasonForm("Delete Refund",
            $"Delete refund {refund.RefundNumber} of {Money.Format(refund.Amount)} to {refund.StudentName}? Use this only for a " +
            "refund entered by mistake. The fee and the paid amount go back up, and the deletion is kept in the change history.",
            "Delete");
        if (ask.ShowDialog(owner) != DialogResult.OK)
        {
            return false;
        }
        string reason = ask.Reason;
        try
        {
            RefundService.Delete(refund.RefundId, reason);
            return true;
        }
        catch (ValidationException ex)
        {
            Dialogs.Warning(ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The refund could not be deleted.");
            return false;
        }
    }
}
