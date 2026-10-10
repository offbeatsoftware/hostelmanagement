using System.Data.OleDb;
using HostelManagement.Data;
using HostelManagement.Utilities;

namespace HostelManagement.Services;

/// <summary>Result of one step of the database check.</summary>
public sealed record DatabaseCheckStep(string Step, bool Passed, string Details);

/// <summary>
/// Verifies that the application can open the database and insert, read, update
/// and delete a record. The test record is written inside a transaction that is
/// rolled back at the end, so the check never leaves data behind.
/// </summary>
public static class DatabaseCheckService
{
    public static List<DatabaseCheckStep> Run()
    {
        var steps = new List<DatabaseCheckStep>();

        OleDbConnection connection;
        try
        {
            connection = Db.OpenConnection();
            steps.Add(new("Open connection", true, $"Connected using {Db.ProviderName}."));
        }
        catch (Exception ex)
        {
            AppLogger.Error("Database check: open connection failed.", ex);
            steps.Add(new("Open connection", false, ex.Message));
            return steps;
        }

        using (connection)
        {
            steps.Add(CheckTables(connection));

            OleDbTransaction transaction = connection.BeginTransaction();
            try
            {
                RunCrudSteps(connection, transaction, steps);
            }
            catch (Exception ex)
            {
                AppLogger.Error("Database check: test record step failed.", ex);
                steps.Add(new("Test record", false, "The step failed. Technical details were written to the log file."));
            }
            finally
            {
                transaction.Rollback();
                transaction.Dispose();
            }
        }

        return steps;
    }

    private static DatabaseCheckStep CheckTables(OleDbConnection connection)
    {
        HashSet<string> existing = DatabaseInitializer.GetExistingTableNames(connection);
        List<string> missing = DatabaseSchema.Tables
            .Select(table => table.Name)
            .Where(name => !existing.Contains(name))
            .ToList();

        return missing.Count == 0
            ? new("Check tables", true, $"All {DatabaseSchema.Tables.Count} tables are present.")
            : new("Check tables", false, "Missing: " + string.Join(", ", missing));
    }

    // Uses the Hostel table because it does not depend on other tables.
    private static void RunCrudSteps(OleDbConnection connection, OleDbTransaction transaction,
        List<DatabaseCheckStep> steps)
    {
        string testName = $"DATABASE CHECK {Guid.NewGuid():N}"[..30];

        int id = Db.Insert(connection, transaction,
            "INSERT INTO [Hostel] ([HostelName], [Phone], [CreatedDate]) VALUES (?, ?, ?)",
            Db.Param("@HostelName", testName),
            Db.Param("@Phone", "1111111111"),
            Db.Param("@CreatedDate", DateTime.Now));
        steps.Add(new("Insert test record", id > 0, $"Inserted test record with id {id}."));

        string? phone = ReadPhone(connection, transaction, id, testName);
        steps.Add(phone == "1111111111"
            ? new("Read test record", true, "Test record read back with the correct values.")
            : new("Read test record", false, "The test record was not read back correctly."));

        int updated = Db.Execute(connection, transaction,
            "UPDATE [Hostel] SET [Phone] = ? WHERE [HostelId] = ?",
            Db.Param("@Phone", "2222222222"),
            Db.Param("@HostelId", id));
        bool updateOk = updated == 1 && ReadPhone(connection, transaction, id, testName) == "2222222222";
        steps.Add(new("Update test record", updateOk,
            updateOk ? "Test record updated and verified." : "The update could not be verified."));

        int deleted = Db.Execute(connection, transaction,
            "DELETE FROM [Hostel] WHERE [HostelId] = ?",
            Db.Param("@HostelId", id));
        bool deleteOk = deleted == 1 && ReadPhone(connection, transaction, id, testName) is null;
        steps.Add(new("Delete test record", deleteOk,
            deleteOk ? "Test record deleted and verified." : "The delete could not be verified."));
    }

    private static string? ReadPhone(OleDbConnection connection, OleDbTransaction transaction, int id, string name) =>
        Db.Query(connection, transaction,
                "SELECT [Phone] FROM [Hostel] WHERE [HostelId] = ? AND [HostelName] = ?",
                record => record.GetText("Phone"),
                Db.Param("@HostelId", id),
                Db.Param("@HostelName", name))
            .FirstOrDefault();
}
