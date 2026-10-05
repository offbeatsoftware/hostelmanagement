using HostelManagement.Forms;
using HostelManagement.Utilities;

namespace HostelManagement;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => ErrorHandler.Handle(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                ErrorHandler.Handle(ex);
            }
        };

        ApplicationConfiguration.Initialize();

        try
        {
            AppPaths.EnsureFolders();
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex,
                "The application could not create its data folders.\n\n" +
                "Please make sure the application folder is not read only and try again.");
            return;
        }

        AppLogger.Info("Application started.");
        Application.Run(new MainForm());
        AppLogger.Info("Application closed.");
    }
}
