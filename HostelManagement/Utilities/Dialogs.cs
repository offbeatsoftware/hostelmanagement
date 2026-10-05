namespace HostelManagement.Utilities;

/// <summary>
/// Standard message boxes so every screen shows messages the same way.
/// </summary>
public static class Dialogs
{
    public static void Info(string message) =>
        MessageBox.Show(message, AppInfo.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Information);

    public static void Warning(string message) =>
        MessageBox.Show(message, AppInfo.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Warning);

    public static void Error(string message) =>
        MessageBox.Show(message, AppInfo.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);

    /// <summary>Asks a Yes/No question. "No" is the default button to protect against accidental clicks.</summary>
    public static bool Confirm(string message) =>
        MessageBox.Show(message, AppInfo.ProductName, MessageBoxButtons.YesNo, MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2) == DialogResult.Yes;
}
