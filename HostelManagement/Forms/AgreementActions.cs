using System.Diagnostics;
using HostelManagement.Reports;
using HostelManagement.Services;
using HostelManagement.Utilities;

namespace HostelManagement.Forms;

/// <summary>Creates a student's agreement PDF where the admin chooses (the Agreements folder by default) and opens it.</summary>
internal static class AgreementActions
{
    public static void Download(IWin32Window owner, int studentId)
    {
        try
        {
            DateTime today = DateTime.Today;
            AgreementDocument document = AgreementService.Prepare(studentId, today);

            Directory.CreateDirectory(AppPaths.AgreementsFolder);
            using var dialog = new SaveFileDialog
            {
                Title = "Save agreement as PDF",
                Filter = "PDF file (*.pdf)|*.pdf",
                InitialDirectory = AppPaths.AgreementsFolder,
                FileName = AgreementService.FileName(document, today),
            };
            if (dialog.ShowDialog(owner) != DialogResult.OK)
            {
                return;
            }

            AgreementPdfWriter.Write(document, dialog.FileName);
            Process.Start(new ProcessStartInfo(dialog.FileName) { UseShellExecute = true });
        }
        catch (ValidationException ex)
        {
            Dialogs.Warning(ex.Message);
        }
        catch (IOException ex)
        {
            ErrorHandler.Handle(ex, "The agreement could not be saved. If the file is open in a PDF viewer, close it and try again.");
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The agreement PDF could not be created.");
        }
    }
}
