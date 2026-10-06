using System.Data.OleDb;
using HostelManagement.Data;
using HostelManagement.Models;
using HostelManagement.Utilities;

namespace HostelManagement.Services;

public static class HostelService
{
    /// <summary>Raised after a hostel is added, changed or deleted, so lists and the hostel selector can refresh.</summary>
    public static event EventHandler? HostelsChanged;

    public static List<Hostel> GetHostels() => HostelRepository.GetAll();

    public static Hostel? GetHostel(int hostelId) => HostelRepository.Get(hostelId);

    /// <summary>
    /// Validates and adds a hostel (HostelId 0) together with its Single/Double/Triple sharing
    /// types, or saves changes to an existing hostel.
    /// </summary>
    public static Hostel Save(Hostel input)
    {
        var hostel = new Hostel
        {
            HostelId = input.HostelId,
            HostelName = Validators.Clean(input.HostelName),
            Address = Validators.Clean(input.Address),
            Phone = Validators.Clean(input.Phone),
            Email = Validators.Clean(input.Email),
            BillingFrequency = input.BillingFrequency,
        };
        Validate(hostel);

        try
        {
            if (hostel.HostelId == 0)
            {
                hostel.CreatedDate = DateTime.Now;
                hostel.HostelId = Db.InTransaction((connection, transaction) =>
                {
                    int id = HostelRepository.Insert(connection, transaction, hostel);
                    SharingTypeRepository.InsertDefaults(connection, transaction, id);
                    ServiceItemRepository.InsertDefaults(connection, transaction, id);
                    return id;
                });
            }
            else
            {
                hostel.UpdatedDate = DateTime.Now;
                if (HostelRepository.Update(hostel) == 0)
                {
                    throw new ValidationException("This hostel no longer exists. It may have been deleted.");
                }
            }
        }
        catch (OleDbException ex) when (Db.IsDuplicateKeyError(ex))
        {
            throw DuplicateName(hostel.HostelName);
        }

        HostelsChanged?.Invoke(null, EventArgs.Empty);
        return HostelRepository.Get(hostel.HostelId) ?? hostel;
    }

    /// <summary>Deletes a hostel that has no colleges and no rooms.</summary>
    public static void Delete(int hostelId)
    {
        Hostel hostel = HostelRepository.Get(hostelId)
            ?? throw new ValidationException("This hostel no longer exists.");

        if (hostel.CollegeCount > 0 || hostel.RoomCount > 0)
        {
            throw new ValidationException(
                $"\"{hostel.HostelName}\" cannot be deleted because it has {hostel.CollegeCount} college(s) " +
                $"and {hostel.RoomCount} room(s). Delete those first.");
        }

        Db.InTransaction((connection, transaction) =>
        {
            SharingTypeRepository.DeleteForHostel(connection, transaction, hostelId);
            ServiceItemRepository.DeleteForHostel(connection, transaction, hostelId);
            HostelRepository.Delete(connection, transaction, hostelId);
        });

        HostelsChanged?.Invoke(null, EventArgs.Empty);
    }

    private static void Validate(Hostel hostel)
    {
        if (hostel.HostelName.Length == 0)
        {
            throw new ValidationException("Please enter the hostel name.");
        }
        Validators.CheckLength(hostel.HostelName, 150, "Hostel name");
        Validators.CheckLength(hostel.Address, 255, "Address");
        Validators.CheckLength(hostel.Phone, 20, "Phone");
        Validators.CheckLength(hostel.Email, 150, "Email");

        if (!BillingFrequency.All.Contains(hostel.BillingFrequency))
        {
            throw new ValidationException("Please select how often the hostel bills: twice a year or 4 times a year.");
        }
        if (!Validators.IsValidPhoneOrEmpty(hostel.Phone))
        {
            throw new ValidationException("Please enter a valid phone number (digits, spaces, + and - only).");
        }
        if (!Validators.IsValidEmailOrEmpty(hostel.Email))
        {
            throw new ValidationException("Please enter a valid email address, for example office@myhostel.com.");
        }
        if (HostelRepository.NameExists(hostel.HostelName, hostel.HostelId))
        {
            throw DuplicateName(hostel.HostelName);
        }
    }

    private static ValidationException DuplicateName(string name) =>
        new($"A hostel named \"{name}\" already exists.");
}
