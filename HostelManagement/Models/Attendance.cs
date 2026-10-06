namespace HostelManagement.Models;

/// <summary>
/// One student on the night attendance sheet of a date (client decision: attendance once a day at night,
/// marked by the admin). Students in a room that night are listed; a saved record holds Present or Absent.
/// </summary>
public sealed class AttendanceEntry
{
    public int? AttendanceId { get; set; }
    public int StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string RoomNumber { get; set; } = string.Empty;
    public int? BedNumber { get; set; }
    public string ParentName { get; set; } = string.Empty;
    public string ParentEmail { get; set; } = string.Empty;
    public string ParentMobile { get; set; } = string.Empty;

    /// <summary>Present unless the admin marks the student absent.</summary>
    public bool IsPresent { get; set; } = true;
    public string Remarks { get; set; } = string.Empty;

    /// <summary>When the parent was emailed about the absence, if at all.</summary>
    public DateTime? ParentEmailedDate { get; set; }

    public bool IsSaved => AttendanceId is not null;

    public string RoomAndBed => BedNumber is int bed ? $"{RoomNumber}, bed {bed}" : RoomNumber;

    public string EmailedText => ParentEmailedDate is DateTime sent ? $"Emailed {sent:dd MMM HH:mm}" : "Not emailed";
}

/// <summary>The attendance sheet of a hostel for one date.</summary>
public sealed class AttendanceSheet
{
    public DateTime Date { get; init; }
    public List<AttendanceEntry> Entries { get; init; } = [];

    /// <summary>True when attendance has been saved for this date.</summary>
    public bool IsMarked => Entries.Any(e => e.IsSaved);

    public int PresentCount => Entries.Count(e => e.IsPresent);
    public int AbsentCount => Entries.Count(e => !e.IsPresent);
}
