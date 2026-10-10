using System.Diagnostics;
using HostelManagement.Reports;
using HostelManagement.Services;
using HostelManagement.Utilities;

namespace HostelManagement.Forms;

/// <summary>Creates the I-cards of the selected students as a PDF where the admin chooses, and opens it for printing.</summary>
internal static class IdCardActions
{
    public static void Download(IWin32Window owner, IReadOnlyList<int> studentIds, bool transport)
    {
        try
        {
            DateTime today = DateTime.Today;
            (List<IdCard> cards, List<string> problems) = IdCardService.Prepare(studentIds, transport, today);
            if (cards.Count == 0)
            {
                Dialogs.Warning("No I-card could be made:" + Environment.NewLine + Environment.NewLine +
                                string.Join(Environment.NewLine, problems.Select(p => "• " + p)));
                return;
            }

            Directory.CreateDirectory(AppPaths.ReportsFolder);
            using var dialog = new SaveFileDialog
            {
                Title = transport ? "Save transport I-cards as PDF" : "Save I-cards as PDF",
                Filter = "PDF file (*.pdf)|*.pdf",
                InitialDirectory = AppPaths.ReportsFolder,
                FileName = IdCardService.FileName(transport, today),
            };
            if (dialog.ShowDialog(owner) != DialogResult.OK)
            {
                return;
            }

            IdCardPdfWriter.Write(cards, transport, dialog.FileName);
            if (problems.Count > 0)
            {
                Dialogs.Warning($"{cards.Count} I-card(s) were made. Please note:" + Environment.NewLine + Environment.NewLine +
                                string.Join(Environment.NewLine, problems.Select(p => "• " + p)));
            }
            Process.Start(new ProcessStartInfo(dialog.FileName) { UseShellExecute = true });
        }
        catch (ValidationException ex)
        {
            Dialogs.Warning(ex.Message);
        }
        catch (IOException ex)
        {
            ErrorHandler.Handle(ex, "The I-cards could not be saved. If the file is open in a PDF viewer, close it and try again.");
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The I-cards could not be created.");
        }
    }
}
