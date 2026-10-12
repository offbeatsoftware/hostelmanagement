using System.Globalization;
using HostelManagement.Data;
using HostelManagement.Models;
using HostelManagement.Utilities;

namespace HostelManagement.Services;

/// <summary>
/// The month the academic year starts in, chosen in Application Settings and kept in the AppSetting table.
/// Fees, invoice numbers, the dashboard, reports and I-cards all follow it.
/// </summary>
public static class AcademicYearService
{
    internal const string StartMonthSetting = "AcademicYear.StartMonth";

    /// <summary>Reads the saved start month (July when none is saved) and uses it from now on.</summary>
    public static void Load()
    {
        string saved = AppSettingRepository.GetAll().GetValueOrDefault(StartMonthSetting, string.Empty);
        int month = int.TryParse(saved, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) && value is >= 1 and <= 12
            ? value
            : AcademicYear.DefaultStartMonth;
        AcademicYear.UseStartMonth(month);
    }

    /// <summary>Saves a new start month. Fees already made keep their academic year.</summary>
    public static void SaveStartMonth(int month)
    {
        if (month is < 1 or > 12)
        {
            throw new ValidationException("Please choose the month the academic year starts in.");
        }
        AppSettingRepository.SaveAll(new Dictionary<string, string>
        {
            [StartMonthSetting] = month.ToString(CultureInfo.InvariantCulture),
        });
        AcademicYear.UseStartMonth(month);
    }
}
