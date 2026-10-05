using System.Reflection;

namespace HostelManagement.Utilities;

public static class AppInfo
{
    public const string ProductName = "Hostel Management System";

    /// <summary>The client's business name, shown on the sign in screen.</summary>
    public const string BusinessName = "Shri Balaji Hostel";

    public static string Version { get; } =
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
}
