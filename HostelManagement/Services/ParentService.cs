using HostelManagement.Data;
using HostelManagement.Models;
using HostelManagement.Utilities;

namespace HostelManagement.Services;

/// <summary>
/// Parents and guardians. Every student has at least one parent, and exactly one of them is the
/// primary contact (the one who receives invoices and reminders).
/// </summary>
public static class ParentService
{
    /// <summary>Parents of the hostel's students, ordered by student and primary contact first.</summary>
    public static List<Parent> GetParents(int hostelId) =>
        ParentRepository.GetForHostel(hostelId)
            .OrderBy(p => p.StudentName, StringComparer.CurrentCultureIgnoreCase)
            .ThenByDescending(p => p.IsPrimaryContact)
            .ThenBy(p => p.ParentName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    public static List<Parent> GetParentsOfStudent(int studentId) => ParentRepository.GetForStudent(studentId);

    /// <summary>Adds (ParentId 0) or updates a parent. Marking a parent primary makes the others non primary.</summary>
    public static Parent Save(Parent input)
    {
        Parent parent = Clean(input);
        Validate(parent);

        if (StudentRepository.Get(parent.StudentId) is null)
        {
            throw new ValidationException("Please select the student.");
        }

        List<Parent> existing = ParentRepository.GetForStudent(parent.StudentId);
        if (parent.ParentId > 0)
        {
            Parent current = existing.FirstOrDefault(p => p.ParentId == parent.ParentId)
                ?? throw new ValidationException("This parent no longer exists. It may have been deleted.");
            if (current.IsPrimaryContact && !parent.IsPrimaryContact)
            {
                throw new ValidationException(
                    "Every student needs a primary contact. Mark another parent as the primary contact instead.");
            }
        }
        else if (existing.Count == 0)
        {
            parent.IsPrimaryContact = true;
        }

        Db.InTransaction((connection, transaction) =>
        {
            if (parent.ParentId == 0)
            {
                parent.ParentId = ParentRepository.Insert(connection, transaction, parent);
            }
            else
            {
                ParentRepository.Update(connection, transaction, parent);
            }

            if (parent.IsPrimaryContact)
            {
                ParentRepository.SetPrimary(connection, transaction, parent.StudentId, parent.ParentId);
            }
        });

        return ParentRepository.Get(parent.ParentId) ?? parent;
    }

    /// <summary>Deletes a parent. The student's only parent cannot be deleted.</summary>
    public static void Delete(int parentId)
    {
        Parent parent = ParentRepository.Get(parentId)
            ?? throw new ValidationException("This parent no longer exists.");
        List<Parent> others = ParentRepository.GetForStudent(parent.StudentId)
            .Where(p => p.ParentId != parentId)
            .ToList();

        if (others.Count == 0)
        {
            throw new ValidationException(
                $"{parent.ParentName} is the only parent of {parent.StudentName} and cannot be deleted. " +
                "Every student needs at least one parent or guardian.");
        }

        Db.InTransaction((connection, transaction) =>
        {
            ParentRepository.Delete(connection, transaction, parentId);
            if (parent.IsPrimaryContact)
            {
                ParentRepository.SetPrimary(connection, transaction, parent.StudentId, others[0].ParentId);
            }
        });
    }

    internal static Parent Clean(Parent input) => new()
    {
        ParentId = input.ParentId,
        StudentId = input.StudentId,
        ParentName = Validators.Clean(input.ParentName),
        Relationship = Validators.Clean(input.Relationship),
        Mobile = Validators.Clean(input.Mobile),
        Email = Validators.Clean(input.Email),
        Address = Validators.Clean(input.Address),
        IsPrimaryContact = input.IsPrimaryContact,
    };

    /// <summary>Parent name, mobile and email are required (client decision).</summary>
    internal static void Validate(Parent parent)
    {
        if (parent.ParentName.Length == 0)
        {
            throw new ValidationException("Please enter the parent or guardian's name.");
        }
        if (parent.Mobile.Length == 0)
        {
            throw new ValidationException("Please enter the parent or guardian's mobile number.");
        }
        if (parent.Email.Length == 0)
        {
            throw new ValidationException("Please enter the parent or guardian's email address.");
        }
        Validators.CheckLength(parent.ParentName, 150, "Parent name");
        Validators.CheckLength(parent.Relationship, 50, "Relationship");
        Validators.CheckLength(parent.Mobile, 20, "Parent mobile");
        Validators.CheckLength(parent.Email, 150, "Parent email");
        Validators.CheckLength(parent.Address, 255, "Parent address");

        if (!Validators.IsValidPhoneOrEmpty(parent.Mobile))
        {
            throw new ValidationException("Please enter a valid mobile number for the parent or guardian.");
        }
        if (!Validators.IsValidEmailOrEmpty(parent.Email))
        {
            throw new ValidationException("Please enter a valid email address for the parent or guardian.");
        }
    }
}
