namespace HostelManagement.Models;

/// <summary>Values of Student.Status.</summary>
public static class StudentStatus
{
    public const string Active = "Active";
    public const string Left = "Left";

    public static IReadOnlyList<string> All { get; } = [Active, Left];
}

/// <summary>A student. The student's hostel is the hostel of the student's college.</summary>
public sealed class Student
{
    public static IReadOnlyList<string> Genders { get; } = ["Male", "Female", "Other"];

    public int StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public DateTime? DateOfBirth { get; set; }
    public string Gender { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public int CollegeId { get; set; }
    public string Course { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public string Mobile { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    /// <summary>Photo file, relative to the data folder (for example Photos\Students\S12_20261005.jpg).</summary>
    public string PhotoPath { get; set; } = string.Empty;

    /// <summary>Full 12 digit Aadhaar number (client decision). Shown masked in lists.</summary>
    public string AadhaarNumber { get; set; } = string.Empty;

    /// <summary>Scanned Aadhaar card (image or PDF), relative to the data folder.</summary>
    public string AadhaarCardPath { get; set; } = string.Empty;

    public DateTime AdmissionDate { get; set; } = DateTime.Today;
    public string Status { get; set; } = StudentStatus.Active;
    public string Remarks { get; set; } = string.Empty;

    // Read only values filled in when students are listed.
    public string CollegeName { get; set; } = string.Empty;
    public string ParentName { get; set; } = string.Empty;
    public string ParentMobile { get; set; } = string.Empty;

    /// <summary>Aadhaar number with only the last four digits visible, for lists and reports.</summary>
    public string AadhaarMasked => AadhaarNumber.Length == 12 ? $"XXXX XXXX {AadhaarNumber[8..]}" : string.Empty;
}
