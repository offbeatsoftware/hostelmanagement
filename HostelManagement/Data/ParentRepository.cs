using System.Data;
using System.Data.OleDb;
using HostelManagement.Models;

namespace HostelManagement.Data;

public static class ParentRepository
{
    private const string SelectParents =
        "SELECT p.*, s.[StudentName] FROM ([Parent] AS p " +
        "INNER JOIN [Student] AS s ON p.[StudentId] = s.[StudentId]) " +
        "INNER JOIN [College] AS c ON s.[CollegeId] = c.[CollegeId]";

    /// <summary>Parents of all students of a hostel.</summary>
    public static List<Parent> GetForHostel(int hostelId) =>
        Db.Query(SelectParents + " WHERE c.[HostelId] = ?", Map, Db.Param("@HostelId", hostelId));

    public static List<Parent> GetForStudent(int studentId) =>
        Db.Query(SelectParents + " WHERE p.[StudentId] = ? ORDER BY p.[IsPrimaryContact], p.[ParentName]", Map,
            Db.Param("@StudentId", studentId));

    public static Parent? Get(int parentId) =>
        Db.Query(SelectParents + " WHERE p.[ParentId] = ?", Map, Db.Param("@ParentId", parentId)).FirstOrDefault();

    public static int Insert(OleDbConnection connection, OleDbTransaction transaction, Parent parent) =>
        Db.Insert(connection, transaction,
            "INSERT INTO [Parent] ([StudentId], [ParentName], [Relationship], [Mobile], [Email], [Address], " +
            "[IsPrimaryContact]) VALUES (?, ?, ?, ?, ?, ?, ?)",
            Db.Param("@StudentId", parent.StudentId),
            Db.Param("@ParentName", parent.ParentName),
            Db.OptionalText("@Relationship", parent.Relationship),
            Db.Param("@Mobile", parent.Mobile),
            Db.Param("@Email", parent.Email),
            Db.OptionalText("@Address", parent.Address),
            Db.Param("@IsPrimaryContact", parent.IsPrimaryContact));

    public static void Update(OleDbConnection connection, OleDbTransaction transaction, Parent parent) =>
        Db.Execute(connection, transaction,
            "UPDATE [Parent] SET [ParentName] = ?, [Relationship] = ?, [Mobile] = ?, [Email] = ?, [Address] = ?, " +
            "[IsPrimaryContact] = ? WHERE [ParentId] = ?",
            Db.Param("@ParentName", parent.ParentName),
            Db.OptionalText("@Relationship", parent.Relationship),
            Db.Param("@Mobile", parent.Mobile),
            Db.Param("@Email", parent.Email),
            Db.OptionalText("@Address", parent.Address),
            Db.Param("@IsPrimaryContact", parent.IsPrimaryContact),
            Db.Param("@ParentId", parent.ParentId));

    /// <summary>Makes one parent the student's only primary contact.</summary>
    public static void SetPrimary(OleDbConnection connection, OleDbTransaction transaction, int studentId, int parentId) =>
        Db.Execute(connection, transaction,
            "UPDATE [Parent] SET [IsPrimaryContact] = ([ParentId] = ?) WHERE [StudentId] = ?",
            Db.Param("@ParentId", parentId),
            Db.Param("@StudentId", studentId));

    public static void Delete(OleDbConnection connection, OleDbTransaction transaction, int parentId) =>
        Db.Execute(connection, transaction, "DELETE FROM [Parent] WHERE [ParentId] = ?",
            Db.Param("@ParentId", parentId));

    public static void DeleteForStudent(OleDbConnection connection, OleDbTransaction transaction, int studentId) =>
        Db.Execute(connection, transaction, "DELETE FROM [Parent] WHERE [StudentId] = ?",
            Db.Param("@StudentId", studentId));

    private static Parent Map(IDataRecord record) => new()
    {
        ParentId = record.GetInt("ParentId"),
        StudentId = record.GetInt("StudentId"),
        ParentName = record.GetText("ParentName"),
        Relationship = record.GetText("Relationship"),
        Mobile = record.GetText("Mobile"),
        Email = record.GetText("Email"),
        Address = record.GetText("Address"),
        IsPrimaryContact = record.GetBool("IsPrimaryContact"),
        StudentName = record.GetText("StudentName"),
    };
}
