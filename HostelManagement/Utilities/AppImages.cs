namespace HostelManagement.Utilities;

/// <summary>Pictures built into the program (embedded resources).</summary>
public static class AppImages
{
    private static Icon? s_appIcon;

    /// <summary>The application icon (SB monogram) for windows and the task bar, or null if it cannot be loaded.</summary>
    public static Icon? AppIcon
    {
        get
        {
            if (s_appIcon is not null)
            {
                return s_appIcon;
            }
            try
            {
                using Stream? stream = typeof(AppImages).Assembly.GetManifestResourceStream("HostelManagement.AppIcon.ico");
                return s_appIcon = stream is null ? null : new Icon(stream);
            }
            catch (Exception ex) when (ex is ArgumentException or IOException)
            {
                AppLogger.Error("Could not load the application icon.", ex);
                return null;
            }
        }
    }

    /// <summary>The hostel photo, or null if it cannot be loaded. The caller disposes it.</summary>
    public static Image? LoadHostelPhoto()
    {
        try
        {
            using Stream? stream = typeof(AppImages).Assembly.GetManifestResourceStream("HostelManagement.LoginBackground.jpg");
            if (stream is null)
            {
                return null;
            }
            using var image = Image.FromStream(stream);
            return new Bitmap(image);
        }
        catch (Exception ex) when (ex is ArgumentException or OutOfMemoryException or IOException)
        {
            AppLogger.Error("Could not load the hostel photo.", ex);
            return null;
        }
    }
}
