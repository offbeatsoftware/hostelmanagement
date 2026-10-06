using System.Data.OleDb;
using HostelManagement.Data;
using HostelManagement.Models;
using HostelManagement.Utilities;

namespace HostelManagement.Services;

/// <summary>Room and rent rules.</summary>
public static class RoomService
{
    public const decimal MaxRent = 1_000_000m;

    /// <summary>The hostel's Single/Double/Triple sharing types with their rent.</summary>
    public static List<SharingType> GetSharingTypes(int hostelId) => SharingTypeRepository.GetForHostel(hostelId);

    /// <summary>The hostel's rooms, sorted by room number (2 before 10).</summary>
    public static List<Room> GetRooms(int hostelId) =>
        RoomRepository.GetForHostel(hostelId).OrderBy(room => room.RoomNumber, NaturalComparer.Instance).ToList();

    /// <summary>Sets the rent for a sharing type of one hostel. It applies to every room of that type in the hostel.</summary>
    public static void UpdateRent(int sharingTypeId, decimal rent)
    {
        if (rent <= 0)
        {
            throw new ValidationException("Rent must be more than zero.");
        }
        if (rent > MaxRent)
        {
            throw new ValidationException($"Rent cannot be more than {Money.Format(MaxRent)}.");
        }
        if (decimal.Round(rent, 2) != rent)
        {
            throw new ValidationException("Rent can have at most two decimal places.");
        }
        if (SharingTypeRepository.UpdateRent(sharingTypeId, rent) == 0)
        {
            throw new ValidationException("This sharing type no longer exists.");
        }
    }

    /// <summary>
    /// Validates and adds a room to input.HostelId, or saves changes to an existing one
    /// (RoomId > 0; it stays in its hostel).
    /// </summary>
    public static Room Save(Room input)
    {
        var room = new Room
        {
            RoomId = input.RoomId,
            HostelId = input.HostelId,
            RoomNumber = Validators.Clean(input.RoomNumber),
            Floor = Validators.Clean(input.Floor),
            SharingTypeId = input.SharingTypeId,
            Gender = Validators.Clean(input.Gender),
            IsActive = input.IsActive,
            Remarks = Validators.Clean(input.Remarks),
        };
        Validate(room);

        try
        {
            if (room.RoomId == 0)
            {
                room.RoomId = RoomRepository.Insert(room);
            }
            else if (RoomRepository.Update(room) == 0)
            {
                throw new ValidationException("This room no longer exists. It may have been deleted.");
            }
        }
        catch (OleDbException ex) when (Db.IsDuplicateKeyError(ex))
        {
            throw DuplicateNumber(room.RoomNumber);
        }

        return RoomRepository.Get(room.RoomId) ?? room;
    }

    /// <summary>
    /// Deletes a room that has never had a student. Rooms with allocation history are kept
    /// for the records; mark them inactive instead.
    /// </summary>
    public static void Delete(int roomId)
    {
        if (RoomRepository.CountAllocations(roomId) > 0)
        {
            throw new ValidationException(
                "This room cannot be deleted because students have stayed in it. " +
                "Edit the room and mark it inactive instead.");
        }

        RoomRepository.Delete(roomId);
    }

    /// <summary>
    /// Checks that a student can be allocated to the room now: the room exists, is active and
    /// has a free bed. Used by Room Allocation.
    /// </summary>
    public static Room CheckCanAllocate(int roomId)
    {
        Room room = RoomRepository.Get(roomId)
            ?? throw new ValidationException("This room no longer exists.");

        if (!room.IsActive)
        {
            throw new ValidationException($"Room {room.RoomNumber} is inactive. Students cannot be allocated to it.");
        }
        if (room.Available == 0)
        {
            throw new ValidationException(
                $"Room {room.RoomNumber} is full ({room.Occupied} of {room.Capacity} beds occupied).");
        }
        return room;
    }

    private static void Validate(Room room)
    {
        if (room.RoomNumber.Length == 0)
        {
            throw new ValidationException("Please enter the room number.");
        }
        Validators.CheckLength(room.RoomNumber, 20, "Room number");
        Validators.CheckLength(room.Floor, 20, "Floor");
        Validators.CheckLength(room.Remarks, 255, "Remarks");

        if (!RoomGender.All.Contains(room.Gender))
        {
            throw new ValidationException("Please select whether the room is for boys or girls.");
        }

        Room? existing = null;
        if (room.RoomId > 0)
        {
            existing = RoomRepository.Get(room.RoomId)
                ?? throw new ValidationException("This room no longer exists. It may have been deleted.");
            room.HostelId = existing.HostelId;
        }
        else if (HostelService.GetHostel(room.HostelId) is null)
        {
            throw new ValidationException("Please select a hostel first.");
        }

        SharingType? sharingType = SharingTypeRepository.Get(room.SharingTypeId);
        if (sharingType is null || sharingType.HostelId != room.HostelId)
        {
            throw new ValidationException("Please select the sharing type.");
        }

        if (RoomRepository.NumberExists(room.HostelId, room.RoomNumber, room.RoomId))
        {
            throw DuplicateNumber(room.RoomNumber);
        }

        if (existing is not null)
        {
            if (existing.Occupied > sharingType.Capacity)
            {
                throw new ValidationException(
                    $"Room {existing.RoomNumber} has {existing.Occupied} students, so it cannot be changed to " +
                    $"{sharingType.SharingName} sharing ({sharingType.Capacity} " +
                    $"{(sharingType.Capacity == 1 ? "bed" : "beds")}). Move students out first.");
            }
            if (room.Gender != existing.Gender && existing.Occupied > 0)
            {
                throw new ValidationException(
                    $"Room {existing.RoomNumber} has {existing.Occupied} student(s), so it cannot be changed from " +
                    $"{existing.RoomFor} to {RoomGender.DisplayName(room.Gender)}. Move the students out first.");
            }
            if (!room.IsActive && existing.Occupied > 0)
            {
                throw new ValidationException(
                    $"Room {existing.RoomNumber} has {existing.Occupied} student(s) and cannot be made inactive. " +
                    "Check out or transfer the students first.");
            }
        }
    }

    private static ValidationException DuplicateNumber(string roomNumber) =>
        new($"Room number \"{roomNumber}\" already exists in this hostel.");
}
