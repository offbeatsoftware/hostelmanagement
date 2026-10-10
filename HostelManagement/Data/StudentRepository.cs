using System.Data;
using System.Data.OleDb;
using HostelManagement.Models;

namespace HostelManagement.Data;

public static class StudentRepository
{
    private const string SelectStudents =
        "SELECT s.*, c.[CollegeName] FROM [Student] AS s INNER JOIN [College] AS c ON s.[CollegeId] = c.[CollegeId]";

    /// <summary>Students of a hostel (through their college).</summary>
    public static List<Student> GetForHostel(int hostelId) =>
        Db.Query(SelectStudents + " WHERE c.[HostelId] = ?", Map, Db.Param("@HostelId", hostelId));

    public static Student? Get(int studentId) =>
        Db.Query(SelectStudents + " WHERE s.[StudentId] = ?", Map, Db.Param("@StudentId", studentId)).FirstOrDefault();

    public static int Insert(OleDbConnection connection, OleDbTransaction transaction, Student student) =>
        Db.Insert(connection, transaction,
            "INSERT INTO [Student] ([StudentName], [DateOfBirth], [Gender], [Address], [CollegeId], [Course], " +
            "[ClassName], [Mobile], [Email], [FatherName], [FatherMobile], [FatherEmail], [MotherName], [MotherMobile], " +
            "[MotherEmail], [AadhaarNumber], [AdmissionDate], [Status], [Remarks]) " +
            "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)",
            Parameters(student));

    public static void Update(OleDbConnection connection, OleDbTransaction transaction, Student student) =>
        Db.Execute(connection, transaction,
            "UPDATE [Student] SET [StudentName] = ?, [DateOfBirth] = ?, [Gender] = ?, [Address] = ?, [CollegeId] = ?, " +
            "[Course] = ?, [ClassName] = ?, [Mobile] = ?, [Email] = ?, [FatherName] = ?, [FatherMobile] = ?, " +
            "[FatherEmail] = ?, [MotherName] = ?, [MotherMobile] = ?, [MotherEmail] = ?, [AadhaarNumber] = ?, [AdmissionDate] = ?, " +
            "[Status] = ?, [Remarks] = ? WHERE [StudentId] = ?",
            [.. Parameters(student), Db.Param("@StudentId", student.StudentId)]);

    public static void UpdateFiles(int studentId, string photoPath, string aadhaarCardPath) =>
        Db.Execute("UPDATE [Student] SET [PhotoPath] = ?, [AadhaarCardPath] = ? WHERE [StudentId] = ?",
            Db.OptionalText("@PhotoPath", photoPath),
            Db.OptionalText("@AadhaarCardPath", aadhaarCardPath),
            Db.Param("@StudentId", studentId));

    public static void Delete(OleDbConnection connection, OleDbTransaction transaction, int studentId) =>
        Db.Execute(connection, transaction, "DELETE FROM [Student] WHERE [StudentId] = ?",
            Db.Param("@StudentId", studentId));

    /// <summary>True when another student already has this Aadhaar number.</summary>
    public static bool AadhaarExists(string aadhaarNumber, int exceptStudentId) =>
        Convert.ToInt32(Db.Scalar(
            "SELECT COUNT(*) FROM [Student] WHERE [AadhaarNumber] = ? AND [StudentId] <> ?",
            Db.Param("@AadhaarNumber", aadhaarNumber),
            Db.Param("@StudentId", exceptStudentId))) > 0;

    /// <summary>Room allocations, invoices, payments, emails and attendance that keep a student's record in use.</summary>
    public static int CountHistory(int studentId)
    {
        int total = 0;
        foreach (string table in new[] { "RoomAllocation", "Invoice", "Payment", "EmailHistory", "Attendance" })
        {
            total += Convert.ToInt32(Db.Scalar($"SELECT COUNT(*) FROM [{table}] WHERE [StudentId] = ?",
                Db.Param("@StudentId", studentId)));
        }
        return total;
    }

    private static OleDbParameter[] Parameters(Student student) =>
    [
        Db.Param("@StudentName", student.StudentName),
        Db.Param("@DateOfBirth", student.DateOfBirth),
        Db.OptionalText("@Gender", student.Gender),
        Db.OptionalText("@Address", student.Address),
        Db.Param("@CollegeId", student.CollegeId),
        Db.OptionalText("@Course", student.Course),
        Db.OptionalText("@ClassName", student.ClassName),
        Db.Param("@Mobile", student.Mobile),
        Db.OptionalText("@Email", student.Email),
        Db.Param("@FatherName", student.FatherName),
        Db.Param("@FatherMobile", student.FatherMobile),
        Db.OptionalText("@FatherEmail", student.FatherEmail),
        Db.OptionalText("@MotherName", student.MotherName),
        Db.OptionalText("@MotherMobile", student.MotherMobile),
        Db.OptionalText("@MotherEmail", student.MotherEmail),
        Db.OptionalText("@AadhaarNumber", student.AadhaarNumber),
        Db.Param("@AdmissionDate", student.AdmissionDate),
        Db.Param("@Status", student.Status),
        Db.OptionalText("@Remarks", student.Remarks),
    ];

    private static Student Map(IDataRecord record) => new()
    {
        StudentId = record.GetInt("StudentId"),
        StudentName = record.GetText("StudentName"),
        DateOfBirth = record.GetNullableDate("DateOfBirth"),
        Gender = record.GetText("Gender"),
        Address = record.GetText("Address"),
        CollegeId = record.GetInt("CollegeId"),
        Course = record.GetText("Course"),
        ClassName = record.GetText("ClassName"),
        Mobile = record.GetText("Mobile"),
        Email = record.GetText("Email"),
        FatherName = record.GetText("FatherName"),
        FatherMobile = record.GetText("FatherMobile"),
        FatherEmail = record.GetText("FatherEmail"),
        MotherName = record.GetText("MotherName"),
        MotherMobile = record.GetText("MotherMobile"),
        MotherEmail = record.GetText("MotherEmail"),
        PhotoPath = record.GetText("PhotoPath"),
        AadhaarNumber = record.GetText("AadhaarNumber"),
        AadhaarCardPath = record.GetText("AadhaarCardPath"),
        AdmissionDate = record.GetDate("AdmissionDate"),
        Status = record.GetText("Status"),
        Remarks = record.GetText("Remarks"),
        CollegeName = record.GetText("CollegeName"),
    };
}
