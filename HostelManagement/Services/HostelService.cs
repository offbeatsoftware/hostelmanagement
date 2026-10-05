using HostelManagement.Data;
using HostelManagement.Models;
using HostelManagement.Utilities;

namespace HostelManagement.Services;

public static class HostelService
{
    /// <summary>Raised after the hostel details are saved, so the window title and menu can update.</summary>
    public static event EventHandler? DetailsSaved;

    /// <summary>The saved hostel details, or null when they have not been entered yet.</summary>
    public static HostelDetails? GetDetails() => HostelRepository.Get();

    /// <summary>The hostel name, or empty when not entered yet.</summary>
    public static string GetHostelName() => GetDetails()?.HostelName ?? string.Empty;

    /// <summary>Validates and saves the hostel details (adds the row the first time).</summary>
    public static HostelDetails Save(HostelDetails input)
    {
        var hostel = new HostelDetails
        {
            HostelName = Validators.Clean(input.HostelName),
            Address = Validators.Clean(input.Address),
            Phone = Validators.Clean(input.Phone),
            Email = Validators.Clean(input.Email),
        };
        Validate(hostel);

        HostelDetails? existing = HostelRepository.Get();
        if (existing is null)
        {
            hostel.CreatedDate = DateTime.Now;
            hostel.HostelId = HostelRepository.Insert(hostel);
        }
        else
        {
            hostel.HostelId = existing.HostelId;
            hostel.CreatedDate = existing.CreatedDate;
            hostel.UpdatedDate = DateTime.Now;
            HostelRepository.Update(hostel);
        }

        DetailsSaved?.Invoke(null, EventArgs.Empty);
        return hostel;
    }

    private static void Validate(HostelDetails hostel)
    {
        if (hostel.HostelName.Length == 0)
        {
            throw new ValidationException("Please enter the hostel name.");
        }
        Validators.CheckLength(hostel.HostelName, 150, "Hostel name");
        Validators.CheckLength(hostel.Address, 255, "Address");
        Validators.CheckLength(hostel.Phone, 20, "Phone");
        Validators.CheckLength(hostel.Email, 150, "Email");

        if (!Validators.IsValidPhoneOrEmpty(hostel.Phone))
        {
            throw new ValidationException("Please enter a valid phone number (digits, spaces, + and - only).");
        }
        if (!Validators.IsValidEmailOrEmpty(hostel.Email))
        {
            throw new ValidationException("Please enter a valid email address, for example office@myhostel.com.");
        }
    }
}
