using System.Reflection;

namespace HostelManagement.Utilities;

public static class AppInfo
{
    public const string ProductName = "Hostel Management System";

    public static string Version { get; } =
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
}
