using System.Data;
using HostelManagement.Models;

namespace HostelManagement.Data;

public static class CollegeRepository
{
    /// <summary>All colleges ordered by name, optionally filtered by part of the name.</summary>
    public static List<College> Search(string nameContains = "")
    {
        if (nameContains.Length == 0)
        {
            return Db.Query("SELECT * FROM [College] ORDER BY [CollegeName]", Map);
        }

        // Escape Access LIKE wildcards so the admin's text is matched literally.
        string pattern = "%" + nameContains.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]") + "%";
        return Db.Query(
            "SELECT * FROM [College] WHERE [CollegeName] ALIKE ? ORDER BY [CollegeName]",
            Map,
            Db.Param("@Pattern", pattern));
    }

    public static int Insert(College college) =>
        Db.Insert(
            "INSERT INTO [College] ([CollegeName], [Address], [Phone]) VALUES (?, ?, ?)",
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

    /// <summary>True when another college already has this name (ignoring case).</summary>
    public static bool NameExists(string collegeName, int exceptCollegeId) =>
        Convert.ToInt32(Db.Scalar(
            "SELECT COUNT(*) FROM [College] WHERE UCASE([CollegeName]) = UCASE(?) AND [CollegeId] <> ?",
            Db.Param("@CollegeName", collegeName),
            Db.Param("@CollegeId", exceptCollegeId))) > 0;

    public static int CountStudents(int collegeId) =>
        Convert.ToInt32(Db.Scalar(
            "SELECT COUNT(*) FROM [Student] WHERE [CollegeId] = ?",
            Db.Param("@CollegeId", collegeId)));

    private static College Map(IDataRecord record) => new()
    {
        CollegeId = record.GetInt("CollegeId"),
        CollegeName = record.GetText("CollegeName"),
        Address = record.GetText("Address"),
        Phone = record.GetText("Phone"),
    };
}
