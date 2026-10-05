namespace HostelManagement.Utilities;

/// <summary>Pictures built into the program (embedded resources).</summary>
public static class AppImages
{
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
