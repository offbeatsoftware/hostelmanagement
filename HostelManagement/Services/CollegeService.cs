using System.Data.OleDb;
using HostelManagement.Data;
using HostelManagement.Models;
using HostelManagement.Utilities;

namespace HostelManagement.Services;

public static class CollegeService
{
    /// <summary>The hostel's colleges, optionally filtered by part of the name.</summary>
    public static List<College> Search(int hostelId, string? nameContains = null) =>
        CollegeRepository.Search(hostelId, Validators.Clean(nameContains));

    /// <summary>
    /// Validates and adds a new college to input.HostelId, or saves changes to an existing one
    /// (CollegeId > 0; it stays in its hostel).
    /// </summary>
    public static College Save(College input)
    {
        var college = new College
        {
            CollegeId = input.CollegeId,
            HostelId = input.HostelId,
            CollegeName = Validators.Clean(input.CollegeName),
            Address = Validators.Clean(input.Address),
            Phone = Validators.Clean(input.Phone),
        };
        Validate(college);

        try
        {
            if (college.CollegeId == 0)
            {
                college.CollegeId = CollegeRepository.Insert(college);
            }
            else if (CollegeRepository.Update(college) == 0)
            {
                throw new ValidationException("This college no longer exists. It may have been deleted.");
            }
        }
        catch (OleDbException ex) when (Db.IsDuplicateKeyError(ex))
        {
            throw DuplicateName(college.CollegeName);
        }

        return college;
    }

    /// <summary>Deletes a college that no student is linked to.</summary>
    public static void Delete(int collegeId)
    {
        int students = CollegeRepository.CountStudents(collegeId);
        if (students > 0)
        {
            throw new ValidationException(
                $"This college cannot be deleted because {students} student record(s) are linked to it.");
        }

        CollegeRepository.Delete(collegeId);
    }

    private static void Validate(College college)
    {
        if (college.CollegeId > 0)
        {
            College existing = CollegeRepository.Get(college.CollegeId)
                ?? throw new ValidationException("This college no longer exists. It may have been deleted.");
            college.HostelId = existing.HostelId;
        }
        else if (HostelService.GetHostel(college.HostelId) is null)
        {
            throw new ValidationException("Please select a hostel first.");
        }

        if (college.CollegeName.Length == 0)
        {
            throw new ValidationException("Please enter the college name.");
        }
        Validators.CheckLength(college.CollegeName, 150, "College name");
        Validators.CheckLength(college.Address, 255, "Address");
        Validators.CheckLength(college.Phone, 20, "Phone");

        if (!Validators.IsValidPhoneOrEmpty(college.Phone))
        {
            throw new ValidationException("Please enter a valid phone number (digits, spaces, + and - only).");
        }
        if (CollegeRepository.NameExists(college.HostelId, college.CollegeName, college.CollegeId))
        {
            throw DuplicateName(college.CollegeName);
        }
    }

    private static ValidationException DuplicateName(string name) =>
        new($"A college named \"{name}\" already exists in this hostel.");
}
