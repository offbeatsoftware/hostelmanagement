using System.Data;
using HostelManagement.Models;

namespace HostelManagement.Data;

public static class CollegeRepository
{
    /// <summary>The hostel's colleges ordered by name, optionally filtered by part of the name.</summary>
    public static List<College> Search(int hostelId, string nameContains = "")
    {
        if (nameContains.Length == 0)
        {
            return Db.Query("SELECT * FROM [College] WHERE [HostelId] = ? ORDER BY [CollegeName]", Map,
                Db.Param("@HostelId", hostelId));
        }

        // Escape Access LIKE wildcards so the admin's text is matched literally.
        string pattern = "%" + nameContains.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]") + "%";
        return Db.Query(
            "SELECT * FROM [College] WHERE [HostelId] = ? AND [CollegeName] ALIKE ? ORDER BY [CollegeName]",
            Map,
            Db.Param("@HostelId", hostelId),
            Db.Param("@Pattern", pattern));
    }

    public static College? Get(int collegeId) =>
        Db.Query("SELECT * FROM [College] WHERE [CollegeId] = ?", Map, Db.Param("@CollegeId", collegeId))
            .FirstOrDefault();

    public static int Insert(College college) =>
        Db.Insert(
            "INSERT INTO [College] ([HostelId], [CollegeName], [Address], [Phone]) VALUES (?, ?, ?, ?)",
            Db.Param("@HostelId", college.HostelId),
            Db.Param("@CollegeName", college.CollegeName),
            Db.OptionalText("@Address", college.Address),
            Db.OptionalText("@Phone", college.Phone));

    public static int Update(College college) =>
        Db.Execute(
            "UPDATE [College] SET [CollegeName] = ?, [Address] = ?, [Phone] = ? WHERE [CollegeId] = ?",
            Db.Param("@CollegeName", college.CollegeName),
            Db.OptionalText("@Address", college.Address),
            Db.OptionalText("@Phone", college.Phone),
            Db.Param("@CollegeId", college.CollegeId));

    public static int Delete(int collegeId) =>
        Db.Execute("DELETE FROM [College] WHERE [CollegeId] = ?", Db.Param("@CollegeId", collegeId));

    /// <summary>True when another college of the same hostel already has this name (ignoring case).</summary>
    public static bool NameExists(int hostelId, string collegeName, int exceptCollegeId) =>
        Convert.ToInt32(Db.Scalar(
            "SELECT COUNT(*) FROM [College] WHERE [HostelId] = ? AND UCASE([CollegeName]) = UCASE(?) AND [CollegeId] <> ?",
            Db.Param("@HostelId", hostelId),
            Db.Param("@CollegeName", collegeName),
            Db.Param("@CollegeId", exceptCollegeId))) > 0;

    public static int CountStudents(int collegeId) =>
        Convert.ToInt32(Db.Scalar(
            "SELECT COUNT(*) FROM [Student] WHERE [CollegeId] = ?",
            Db.Param("@CollegeId", collegeId)));

    private static College Map(IDataRecord record) => new()
    {
        CollegeId = record.GetInt("CollegeId"),
        HostelId = record.GetInt("HostelId"),
        CollegeName = record.GetText("CollegeName"),
        Address = record.GetText("Address"),
        Phone = record.GetText("Phone"),
    };
}
