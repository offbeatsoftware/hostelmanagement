using System.Data.OleDb;
using HostelManagement.Data;
using HostelManagement.Models;
using HostelManagement.Utilities;

namespace HostelManagement.Services;

/// <summary>
/// Hostel services (Wi-Fi, laundry, transport, ...). Services included in the rent have no charge;
/// extra services have a monthly rate and are charged to the students who use them.
/// </summary>
public static class ServiceItemService
{
    public const decimal MaxMonthlyRate = 100_000m;

    public static List<ServiceItem> GetServices(int hostelId) => ServiceItemRepository.GetForHostel(hostelId);

    /// <summary>Active services that are charged extra, for choosing on the student form.</summary>
    public static List<ServiceItem> GetActiveExtraServices(int hostelId) =>
        GetServices(hostelId).Where(s => s.IsActive && !s.IsIncludedInRent).ToList();

    /// <summary>Active services included in the rent, shown for information.</summary>
    public static List<ServiceItem> GetIncludedServices(int hostelId) =>
        GetServices(hostelId).Where(s => s.IsActive && s.IsIncludedInRent).ToList();

    /// <summary>Validates and adds a service to input.HostelId, or saves changes (ServiceId > 0).</summary>
    public static ServiceItem Save(ServiceItem input)
    {
        var service = new ServiceItem
        {
            ServiceId = input.ServiceId,
            HostelId = input.HostelId,
            ServiceName = Validators.Clean(input.ServiceName),
            IsIncludedInRent = input.IsIncludedInRent,
            MonthlyRate = input.IsIncludedInRent ? 0m : input.MonthlyRate,
            IsActive = input.IsActive,
        };
        Validate(service);

        try
        {
            if (service.ServiceId == 0)
            {
                service.ServiceId = ServiceItemRepository.Insert(service);
            }
            else if (ServiceItemRepository.Update(service) == 0)
            {
                throw new ValidationException("This service no longer exists. It may have been deleted.");
            }
        }
        catch (OleDbException ex) when (Db.IsDuplicateKeyError(ex))
        {
            throw DuplicateName(service.ServiceName);
        }

        return ServiceItemRepository.Get(service.ServiceId) ?? service;
    }

    /// <summary>Deletes a service no student has ever used; otherwise it should be made inactive.</summary>
    public static void Delete(int serviceId)
    {
        if (ServiceItemRepository.CountAllUses(serviceId) > 0)
        {
            throw new ValidationException(
                "This service cannot be deleted because students have used it. Edit it and make it inactive instead.");
        }
        ServiceItemRepository.Delete(serviceId);
    }

    private static void Validate(ServiceItem service)
    {
        if (service.ServiceName.Length == 0)
        {
            throw new ValidationException("Please enter the service name.");
        }
        Validators.CheckLength(service.ServiceName, 100, "Service name");

        if (!service.IsIncludedInRent)
        {
            if (service.MonthlyRate <= 0)
            {
                throw new ValidationException("Please enter the monthly rate for this extra service.");
            }
            if (service.MonthlyRate > MaxMonthlyRate)
            {
                throw new ValidationException($"The monthly rate cannot be more than {Money.Format(MaxMonthlyRate)}.");
            }
            if (decimal.Round(service.MonthlyRate, 2) != service.MonthlyRate)
            {
                throw new ValidationException("The monthly rate can have at most two decimal places.");
            }
        }

        if (service.ServiceId > 0)
        {
            ServiceItem existing = ServiceItemRepository.Get(service.ServiceId)
                ?? throw new ValidationException("This service no longer exists. It may have been deleted.");
            service.HostelId = existing.HostelId;

            if (existing.StudentCount > 0 && (!service.IsActive || service.IsIncludedInRent))
            {
                throw new ValidationException(
                    $"{existing.StudentCount} student(s) use {existing.ServiceName}. Remove it from those students " +
                    "(Services tab of the student) before making it inactive or included in rent.");
            }
        }
        else if (HostelService.GetHostel(service.HostelId) is null)
        {
            throw new ValidationException("Please select a hostel first.");
        }

        if (ServiceItemRepository.NameExists(service.HostelId, service.ServiceName, service.ServiceId))
        {
            throw DuplicateName(service.ServiceName);
        }
    }

    private static ValidationException DuplicateName(string name) =>
        new($"A service named \"{name}\" already exists in this hostel.");
}
