using System.Data.OleDb;
using HostelManagement.Data;
using HostelManagement.Models;
using HostelManagement.Utilities;

namespace HostelManagement.Services;

/// <summary>Room and rent rules.</summary>
public static class RoomService
{
    public const decimal MaxRent = 1_000_000m;

    public static List<SharingType> GetSharingTypes() => SharingTypeRepository.GetAll();

    /// <summary>All rooms, sorted by room number (2 before 10).</summary>
    public static List<Room> GetRooms() =>
        RoomRepository.GetAll().OrderBy(room => room.RoomNumber, NaturalComparer.Instance).ToList();

    /// <summary>Sets the rent for a sharing type. It applies to every room of that type.</summary>
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

    /// <summary>Validates and adds a room, or saves changes to an existing one (RoomId > 0).</summary>
    public static Room Save(Room input)
    {
        var room = new Room
        {
            RoomId = input.RoomId,
            RoomNumber = Validators.Clean(input.RoomNumber),
            Floor = Validators.Clean(input.Floor),
            SharingTypeId = input.SharingTypeId,
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

        SharingType sharingType = SharingTypeRepository.Get(room.SharingTypeId)
            ?? throw new ValidationException("Please select the sharing type.");

        if (RoomRepository.NumberExists(room.RoomNumber, room.RoomId))
        {
            throw DuplicateNumber(room.RoomNumber);
        }

        if (room.RoomId > 0)
        {
            Room existing = RoomRepository.Get(room.RoomId)
                ?? throw new ValidationException("This room no longer exists. It may have been deleted.");

            if (existing.Occupied > sharingType.Capacity)
            {
                throw new ValidationException(
                    $"Room {existing.RoomNumber} has {existing.Occupied} students, so it cannot be changed to " +
                    $"{sharingType.SharingName} sharing ({sharingType.Capacity} " +
                    $"{(sharingType.Capacity == 1 ? "bed" : "beds")}). Move students out first.");
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
        new($"Room number \"{roomNumber}\" already exists.");
}
