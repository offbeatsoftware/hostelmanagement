using HostelManagement.Data;
using HostelManagement.Forms;
using HostelManagement.Services;
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
                $"The application could not create its data folder {AppPaths.DataFolder}.\n\n" +
                "Please make sure the drive is available and the folder is not read only, then try again.");
            return;
        }

        try
        {
            DatabaseInitializer.Initialize();
        }
        catch (DatabaseException ex)
        {
            AppLogger.Error("Database initialization failed.", ex);
            Dialogs.Error(ex.Message);
            return;
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The database could not be prepared. The application will now close.");
            return;
        }

        AppLogger.Info("Application started.");

        using (var login = new LoginForm(GetHostelNameForLogin()))
        {
            if (login.ShowDialog() != DialogResult.OK)
            {
                AppLogger.Info("Sign in cancelled.");
                return;
            }
        }

        Application.Run(new MainForm());
        AppLogger.Info("Application closed.");
    }

    private static string GetHostelNameForLogin()
    {
        try
        {
            return HostelService.GetHostelName();
        }
        catch (Exception ex)
        {
            AppLogger.Error("Could not read the hostel name for the sign in screen.", ex);
            return string.Empty;
        }
    }
}
