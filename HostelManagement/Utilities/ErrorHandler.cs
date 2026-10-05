namespace HostelManagement.Utilities;

/// <summary>
/// Logs the technical details of an error and shows the admin a friendly message
/// without a stack trace.
/// </summary>
public static class ErrorHandler
{
    private const string DefaultMessage =
        "Something went wrong and the action could not be completed.\n\n" +
        "Please try again. If the problem continues, note what you were doing and contact support.";

    public static void Handle(Exception ex, string? userMessage = null)
    {
        AppLogger.Error("Unhandled error", ex);
        Dialogs.Error(userMessage ?? DefaultMessage);
    }
}
