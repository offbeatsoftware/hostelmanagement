using HostelManagement.Data;
using HostelManagement.Models;
using HostelManagement.Utilities;

namespace HostelManagement.Services;

/// <summary>Changes to a student's photo and Aadhaar card made in the student form.</summary>
/// <param name="NewPhotoFile">A photo chosen by the admin, or null to keep the current one.</param>
/// <param name="RemovePhoto">True to remove the current photo.</param>
/// <param name="NewAadhaarCardFile">A scanned Aadhaar card chosen by the admin, or null to keep the current one.</param>
/// <param name="RemoveAadhaarCard">True to remove the current Aadhaar card file.</param>
public sealed record StudentFileChanges(
    string? NewPhotoFile = null,
    bool RemovePhoto = false,
    string? NewAadhaarCardFile = null,
    bool RemoveAadhaarCard = false);

public static class StudentService
{
    /// <summary>Students of a hostel, sorted by name, with their primary parent's name and mobile.</summary>
    public static List<Student> GetStudents(int hostelId)
    {
        List<Student> students = StudentRepository.GetForHostel(hostelId);
        Dictionary<int, Parent> primaryParents = ParentRepository.GetForHostel(hostelId)
            .Where(p => p.IsPrimaryContact)
            .GroupBy(p => p.StudentId)
            .ToDictionary(g => g.Key, g => g.First());

        Dictionary<int, string> rooms = AllocationRepository.GetForHostel(hostelId, includeHistory: false)
            .GroupBy(a => a.StudentId)
            .ToDictionary(g => g.Key, g => g.First().RoomNumber);

        foreach (Student student in students)
        {
            if (primaryParents.TryGetValue(student.StudentId, out Parent? parent))
            {
                student.ParentName = parent.ParentName;
                student.ParentMobile = parent.Mobile;
            }
            student.RoomNumber = rooms.GetValueOrDefault(student.StudentId, string.Empty);
        }

        return students.OrderBy(s => s.StudentName, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public static Student? GetStudent(int studentId) => StudentRepository.Get(studentId);

    /// <summary>The student's primary contact (null only for records created outside the application).</summary>
    public static Parent? GetPrimaryParent(int studentId) =>
        ParentRepository.GetForStudent(studentId).FirstOrDefault(p => p.IsPrimaryContact);

    /// <summary>
    /// Validates and saves a student together with the primary parent, then stores the photo and
    /// Aadhaar card. Everything is checked before anything is saved.
    /// </summary>
    /// <param name="extraServiceIds">
    /// The extra services (such as transport) the student uses, or null to leave them unchanged.
    /// A newly selected service starts on the admission date for a new student and today otherwise;
    /// a removed one ends today.
    /// </param>
    public static Student Save(Student input, Parent primaryParent, StudentFileChanges? files = null,
        IReadOnlyCollection<int>? extraServiceIds = null)
    {
        files ??= new StudentFileChanges();
        Student student = Clean(input);
        Parent parent = ParentService.Clean(primaryParent);

        Student? existing = null;
        if (student.StudentId > 0)
        {
            existing = StudentRepository.Get(student.StudentId)
                ?? throw new ValidationException("This student no longer exists. It may have been deleted.");
        }

        Validate(student, existing);
        ParentService.Validate(parent);
        if (files.NewPhotoFile is not null)
        {
            StudentFileService.ValidatePhoto(files.NewPhotoFile);
        }
        if (files.NewAadhaarCardFile is not null)
        {
            StudentFileService.ValidateAadhaarCard(files.NewAadhaarCardFile);
        }
        if (extraServiceIds is not null)
        {
            ValidateExtraServices(student, extraServiceIds);
        }

        Db.InTransaction((connection, transaction) =>
        {
            if (existing is null)
            {
                student.StudentId = StudentRepository.Insert(connection, transaction, student);
            }
            else
            {
                StudentRepository.Update(connection, transaction, student);
            }

            parent.StudentId = student.StudentId;
            parent.IsPrimaryContact = true;
            if (parent.ParentId == 0)
            {
                parent.ParentId = ParentRepository.Insert(connection, transaction, parent);
            }
            else
            {
                ParentRepository.Update(connection, transaction, parent);
            }
            ParentRepository.SetPrimary(connection, transaction, student.StudentId, parent.ParentId);

            if (extraServiceIds is not null)
            {
                DateTime startDate = existing is null ? student.AdmissionDate : DateTime.Today;
                SaveExtraServices(connection, transaction, student.StudentId, extraServiceIds, startDate);
            }
        });

        SaveFiles(student.StudentId, existing, files);
        return StudentRepository.Get(student.StudentId) ?? student;
    }

    /// <summary>
    /// Deletes a student, their parents and files. Students with room, invoice, payment or email
    /// history are kept for the records; mark them as Left instead.
    /// </summary>
    public static void Delete(int studentId)
    {
        Student student = StudentRepository.Get(studentId)
            ?? throw new ValidationException("This student no longer exists.");

        if (StudentRepository.CountHistory(studentId) > 0)
        {
            throw new ValidationException(
                $"{student.StudentName} has room, invoice or payment records and cannot be deleted. " +
                "Edit the student and set the status to Left instead.");
        }

        Db.InTransaction((connection, transaction) =>
        {
            ParentRepository.DeleteForStudent(connection, transaction, studentId);
            StudentServiceRepository.DeleteForStudent(connection, transaction, studentId);
            StudentRepository.Delete(connection, transaction, studentId);
        });

        StudentFileService.TryDelete(student.PhotoPath);
        StudentFileService.TryDelete(student.AadhaarCardPath);
    }

    /// <summary>The extra services the student uses now.</summary>
    public static List<StudentServiceUse> GetCurrentServices(int studentId) =>
        StudentServiceRepository.GetCurrentForStudent(studentId);

    /// <summary>Selected services must be active extra services of the student's hostel.</summary>
    private static void ValidateExtraServices(Student student, IReadOnlyCollection<int> serviceIds)
    {
        int hostelId = CollegeRepository.Get(student.CollegeId)?.HostelId ?? 0;
        HashSet<int> allowed = ServiceItemService.GetActiveExtraServices(hostelId).Select(s => s.ServiceId).ToHashSet();
        HashSet<int> current = student.StudentId > 0
            ? StudentServiceRepository.GetCurrentForStudent(student.StudentId).Select(u => u.ServiceId).ToHashSet()
            : [];

        // A service already in use may stay selected even if it was made inactive later.
        if (serviceIds.Any(id => !allowed.Contains(id) && !current.Contains(id)))
        {
            throw new ValidationException("Only active extra services of the student's hostel can be selected.");
        }
    }

    private static void SaveExtraServices(System.Data.OleDb.OleDbConnection connection,
        System.Data.OleDb.OleDbTransaction transaction, int studentId, IReadOnlyCollection<int> serviceIds, DateTime startDate)
    {
        List<StudentServiceUse> current = StudentServiceRepository.GetCurrentForStudent(studentId);

        foreach (StudentServiceUse use in current.Where(u => !serviceIds.Contains(u.ServiceId)))
        {
            if (use.StartDate.Date >= DateTime.Today)
            {
                // Started today or later and removed again: it was never really used.
                StudentServiceRepository.Delete(connection, transaction, use.StudentServiceId);
            }
            else
            {
                StudentServiceRepository.Stop(connection, transaction, use.StudentServiceId, DateTime.Today);
            }
        }

        foreach (int serviceId in serviceIds.Distinct().Where(id => current.All(u => u.ServiceId != id)))
        {
            StudentServiceRepository.Start(connection, transaction, studentId, serviceId, startDate.Date);
        }
    }

    private static void SaveFiles(int studentId, Student? existing, StudentFileChanges files)
    {
        string photoPath = existing?.PhotoPath ?? string.Empty;
        string aadhaarCardPath = existing?.AadhaarCardPath ?? string.Empty;
        var replaced = new List<string>();

        if (files.NewPhotoFile is not null || files.RemovePhoto)
        {
            replaced.Add(photoPath);
            photoPath = files.NewPhotoFile is null ? string.Empty : StudentFileService.StorePhoto(studentId, files.NewPhotoFile);
        }
        if (files.NewAadhaarCardFile is not null || files.RemoveAadhaarCard)
        {
            replaced.Add(aadhaarCardPath);
            aadhaarCardPath = files.NewAadhaarCardFile is null
                ? string.Empty
                : StudentFileService.StoreAadhaarCard(studentId, files.NewAadhaarCardFile);
        }

        if (replaced.Count > 0)
        {
            StudentRepository.UpdateFiles(studentId, photoPath, aadhaarCardPath);
            replaced.ForEach(StudentFileService.TryDelete);
        }
    }

    private static Student Clean(Student input) => new()
    {
        StudentId = input.StudentId,
        StudentName = Validators.Clean(input.StudentName),
        DateOfBirth = input.DateOfBirth?.Date,
        Gender = Validators.Clean(input.Gender),
        Address = Validators.Clean(input.Address),
        CollegeId = input.CollegeId,
        Course = Validators.Clean(input.Course),
        ClassName = Validators.Clean(input.ClassName),
        Mobile = Validators.Clean(input.Mobile),
        Email = Validators.Clean(input.Email),
        AadhaarNumber = Validators.CleanAadhaar(input.AadhaarNumber),
        AdmissionDate = input.AdmissionDate.Date,
        Status = Validators.Clean(input.Status),
        Remarks = Validators.Clean(input.Remarks),
    };

    /// <summary>Name, mobile, college and admission date are required (client decision).</summary>
    private static void Validate(Student student, Student? existing)
    {
        if (student.StudentName.Length == 0)
        {
            throw new ValidationException("Please enter the student's name.");
        }
        if (student.Mobile.Length == 0)
        {
            throw new ValidationException("Please enter the student's mobile number.");
        }
        Validators.CheckLength(student.StudentName, 150, "Student name");
        Validators.CheckLength(student.Address, 255, "Address");
        Validators.CheckLength(student.Course, 100, "Course");
        Validators.CheckLength(student.ClassName, 50, "Class");
        Validators.CheckLength(student.Mobile, 20, "Mobile");
        Validators.CheckLength(student.Email, 150, "Email");
        Validators.CheckLength(student.Remarks, 255, "Remarks");

        if (!Validators.IsValidPhoneOrEmpty(student.Mobile))
        {
            throw new ValidationException("Please enter a valid mobile number for the student.");
        }
        if (!Validators.IsValidEmailOrEmpty(student.Email))
        {
            throw new ValidationException("Please enter a valid email address for the student.");
        }
        if (student.Gender.Length > 0 && !Student.Genders.Contains(student.Gender))
        {
            throw new ValidationException("Please select the gender from the list.");
        }
        if (!StudentStatus.All.Contains(student.Status))
        {
            throw new ValidationException("Please select the status (Active or Left).");
        }
        if (student.DateOfBirth is DateTime birth && birth >= DateTime.Today)
        {
            throw new ValidationException("The date of birth must be in the past.");
        }
        if (student.DateOfBirth is DateTime born && student.AdmissionDate <= born)
        {
            throw new ValidationException("The admission date must be after the date of birth.");
        }

        if (!Validators.IsValidAadhaarOrEmpty(student.AadhaarNumber))
        {
            throw new ValidationException(
                "The Aadhaar number is not valid. It must have 12 digits; please check it for typing mistakes.");
        }
        if (student.AadhaarNumber.Length > 0 && StudentRepository.AadhaarExists(student.AadhaarNumber, student.StudentId))
        {
            throw new ValidationException("Another student already has this Aadhaar number.");
        }

        College college = CollegeRepository.Get(student.CollegeId)
            ?? throw new ValidationException("Please select the college.");

        if (existing is not null && AllocationRepository.GetCurrentForStudent(existing.StudentId) is RoomAllocation room)
        {
            if (student.Status == StudentStatus.Left)
            {
                throw new ValidationException(
                    $"{existing.StudentName} is in room {room.RoomNumber}. Use Check-out on the Room Allocation screen; " +
                    "it sets the status to Left.");
            }
            if (student.Gender != existing.Gender)
            {
                throw new ValidationException(
                    $"{existing.StudentName} is in room {room.RoomNumber}, so the gender cannot be changed. Check out first.");
            }
        }
        if (existing is not null &&
            CollegeRepository.Get(existing.CollegeId) is College oldCollege &&
            oldCollege.HostelId != college.HostelId)
        {
            throw new ValidationException("A student cannot be moved to a college of another hostel.");
        }
    }
}
