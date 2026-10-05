namespace HostelManagement.Utilities;

/// <summary>
/// Standard message boxes so every screen shows messages the same way.
/// </summary>
public static class Dialogs
{
    /// <summary>
    /// Set by the automated tests: a message box would wait for a click that never comes,
    /// so it throws instead and the test fails with the message.
    /// </summary>
    internal static bool ThrowInsteadOfShowing { get; set; }

    public static void Info(string message) => Show(message, MessageBoxButtons.OK, MessageBoxIcon.Information);

    public static void Warning(string message) => Show(message, MessageBoxButtons.OK, MessageBoxIcon.Warning);

    public static void Error(string message) => Show(message, MessageBoxButtons.OK, MessageBoxIcon.Error);

    /// <summary>Asks a Yes/No question. "No" is the default button to protect against accidental clicks.</summary>
    public static bool Confirm(string message) =>
        Show(message, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes;

    private static DialogResult Show(string message, MessageBoxButtons buttons, MessageBoxIcon icon,
        MessageBoxDefaultButton defaultButton = MessageBoxDefaultButton.Button1)
    {
        if (ThrowInsteadOfShowing)
        {
            throw new InvalidOperationException($"Message box shown during a test: {message}");
        }
        return MessageBox.Show(message, AppInfo.ProductName, buttons, icon, defaultButton);
    }
}
