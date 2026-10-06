using HostelManagement.Data;
using HostelManagement.Models;
using HostelManagement.Utilities;

namespace HostelManagement.Services;

/// <summary>
/// Check-in, room transfer and check-out (client decisions):
/// a student can only be in one room at a time, of their own hostel and gender, with a free bed;
/// a transfer leaves the old room and enters the new one on the same date;
/// check-out also sets the student's status to Left.
/// </summary>
public static class AllocationService
{
    /// <summary>The hostel's allocations, sorted by room then student; current ones only unless <paramref name="includeHistory"/>.</summary>
    public static List<RoomAllocation> GetAllocations(int hostelId, bool includeHistory = false) =>
        AllocationRepository.GetForHostel(hostelId, includeHistory)
            .OrderBy(a => a.RoomNumber, NaturalComparer.Instance)
            .ThenBy(a => a.StudentName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(a => a.CheckInDate)
            .ToList();

    public static RoomAllocation? GetCurrentAllocation(int studentId) => AllocationRepository.GetCurrentForStudent(studentId);

    public static List<RoomAllocation> GetHistory(int studentId) => AllocationRepository.GetForStudent(studentId);

    /// <summary>Active students of the hostel who are not in a room, sorted by name.</summary>
    public static List<Student> GetStudentsWithoutRoom(int hostelId)
    {
        HashSet<int> inRoom = AllocationRepository.GetForHostel(hostelId, includeHistory: false)
            .Select(a => a.StudentId)
            .ToHashSet();

        return StudentService.GetStudents(hostelId)
            .Where(s => s.Status == StudentStatus.Active && !inRoom.Contains(s.StudentId))
            .ToList();
    }

    /// <summary>Active rooms of the hostel for the gender that have a free bed, except one room (for transfers).</summary>
    public static List<Room> GetRoomsFor(int hostelId, string gender, int exceptRoomId = 0) =>
        RoomService.GetRooms(hostelId)
            .Where(r => r.Gender == gender && r.Available > 0 && r.RoomId != exceptRoomId)
            .ToList();

    /// <summary>Allocates a student to a room.</summary>
    public static RoomAllocation CheckIn(int studentId, int roomId, DateTime date, string? remarks = null)
    {
        date = date.Date;
        Student student = GetStudentForAllocation(studentId);
        if (AllocationRepository.GetCurrentForStudent(studentId) is RoomAllocation current)
        {
            throw new ValidationException(
                $"{student.StudentName} is already in room {current.RoomNumber}. Use Transfer to move the student.");
        }
        if (date < student.AdmissionDate.Date)
        {
            throw new ValidationException(
                $"The check-in date cannot be before the admission date ({student.AdmissionDate:dd MMM yyyy}).");
        }
        CheckNotInFuture(date, "check-in");
        string cleanRemarks = CleanRemarks(remarks);
        CheckRoomFor(student, roomId);

        int allocationId = Db.InTransaction((connection, transaction) =>
            AllocationRepository.Insert(connection, transaction, new RoomAllocation
            {
                StudentId = studentId,
                RoomId = roomId,
                CheckInDate = date,
                Remarks = cleanRemarks,
            }));

        AppLogger.Info($"Checked in student {studentId} to room {roomId} (allocation {allocationId}).");
        return AllocationRepository.GetCurrentForStudent(studentId)!;
    }

    /// <summary>Moves a student to another room: the old allocation ends and the new one starts on the same date.</summary>
    public static RoomAllocation Transfer(int studentId, int newRoomId, DateTime date, string? remarks = null)
    {
        date = date.Date;
        Student student = GetStudentForAllocation(studentId);
        RoomAllocation current = AllocationRepository.GetCurrentForStudent(studentId)
            ?? throw new ValidationException($"{student.StudentName} is not in a room. Use Check-in instead.");

        if (newRoomId == current.RoomId)
        {
            throw new ValidationException($"{student.StudentName} is already in room {current.RoomNumber}.");
        }
        CheckDateAfterCheckIn(date, current, "transfer");
        CheckNotInFuture(date, "transfer");
        string cleanRemarks = CleanRemarks(remarks);
        CheckRoomFor(student, newRoomId);

        Db.InTransaction((connection, transaction) =>
        {
            if (AllocationRepository.Close(connection, transaction, current.AllocationId, date,
                    AllocationStatus.Transferred, JoinRemarks(current.Remarks, cleanRemarks)) == 0)
            {
                throw new ValidationException("The student's room changed in the meantime. Please refresh and try again.");
            }
            AllocationRepository.Insert(connection, transaction, new RoomAllocation
            {
                StudentId = studentId,
                RoomId = newRoomId,
                CheckInDate = date,
                Remarks = cleanRemarks,
            });
        });

        AppLogger.Info($"Transferred student {studentId} from room {current.RoomId} to room {newRoomId}.");
        return AllocationRepository.GetCurrentForStudent(studentId)!;
    }

    /// <summary>Checks a student out of their room and sets the student's status to Left.</summary>
    public static void CheckOut(int studentId, DateTime date, string? remarks = null)
    {
        date = date.Date;
        Student student = StudentService.GetStudent(studentId)
            ?? throw new ValidationException("This student no longer exists.");
        RoomAllocation current = AllocationRepository.GetCurrentForStudent(studentId)
            ?? throw new ValidationException($"{student.StudentName} is not in a room.");

        CheckDateAfterCheckIn(date, current, "check-out");
        CheckNotInFuture(date, "check-out");
        string cleanRemarks = CleanRemarks(remarks);

        Db.InTransaction((connection, transaction) =>
        {
            if (AllocationRepository.Close(connection, transaction, current.AllocationId, date,
                    AllocationStatus.CheckedOut, JoinRemarks(current.Remarks, cleanRemarks)) == 0)
            {
                throw new ValidationException("The student's room changed in the meantime. Please refresh and try again.");
            }
            AllocationRepository.SetStudentStatus(connection, transaction, studentId, StudentStatus.Left);
        });

        AppLogger.Info($"Checked out student {studentId} from room {current.RoomId}.");
    }

    private static Student GetStudentForAllocation(int studentId)
    {
        Student student = StudentService.GetStudent(studentId)
            ?? throw new ValidationException("Please select the student.");

        if (student.Status != StudentStatus.Active)
        {
            throw new ValidationException(
                $"{student.StudentName}'s status is {student.Status}. Set the status to Active on the Students screen first.");
        }
        if (!RoomGender.All.Contains(student.Gender))
        {
            throw new ValidationException(
                $"Please set {student.StudentName}'s gender (Male or Female) on the Students screen first. " +
                "Rooms are for boys or girls.");
        }
        return student;
    }

    /// <summary>The room must be in the student's hostel, for the student's gender, active and with a free bed.</summary>
    private static void CheckRoomFor(Student student, int roomId)
    {
        Room room = RoomService.CheckCanAllocate(roomId);
        College college = CollegeRepository.Get(student.CollegeId)
            ?? throw new ValidationException("The student's college no longer exists.");

        if (room.HostelId != college.HostelId)
        {
            throw new ValidationException($"Room {room.RoomNumber} belongs to another hostel.");
        }
        if (room.Gender != student.Gender)
        {
            throw new ValidationException(
                $"Room {room.RoomNumber} is for {room.RoomFor.ToLowerInvariant()}. " +
                $"{student.StudentName} cannot be allocated to it.");
        }
    }

    private static void CheckDateAfterCheckIn(DateTime date, RoomAllocation current, string action)
    {
        if (date < current.CheckInDate.Date)
        {
            throw new ValidationException(
                $"The {action} date cannot be before the check-in date ({current.CheckInDate:dd MMM yyyy}).");
        }
    }

    private static void CheckNotInFuture(DateTime date, string action)
    {
        if (date > DateTime.Today)
        {
            throw new ValidationException($"The {action} date cannot be in the future.");
        }
    }

    private static string CleanRemarks(string? remarks)
    {
        string clean = Validators.Clean(remarks);
        Validators.CheckLength(clean, 255, "Remarks");
        return clean;
    }

    /// <summary>Keeps the check-in remarks and adds the closing remarks, within the column size.</summary>
    private static string JoinRemarks(string existing, string added)
    {
        string joined = existing.Length == 0 ? added : added.Length == 0 ? existing : $"{existing} | {added}";
        return joined.Length <= 255 ? joined : joined[..255];
    }
}
