using System.Data.OleDb;
using HostelManagement.Data;
using HostelManagement.Utilities;

namespace HostelManagement.Tests;

/// <summary>
/// Base class for tests that need a database. Each test gets a brand new database
/// in its own temporary folder, so the real C:\HostelData is never touched.
/// </summary>
public abstract class TestDatabase : IDisposable
{
    protected TestDatabase()
    {
        DataFolder = Path.Combine(Path.GetTempPath(), "HostelManagementTests", Guid.NewGuid().ToString("N"));
        AppPaths.UseDataFolder(DataFolder);
        AppPaths.EnsureFolders();
        DatabaseInitializer.Initialize();
    }

    protected string DataFolder { get; }

    protected static int Count(string table) =>
        Convert.ToInt32(Db.Scalar($"SELECT COUNT(*) FROM [{table}]"));

    protected static int AddStudent(string name = "Test Student") =>
        Db.Insert("INSERT INTO [Student] ([StudentName], [Status]) VALUES (?, ?)",
            Db.Param("@StudentName", name),
            Db.Param("@Status", "Active"));

    public void Dispose()
    {
        // Release pooled connections so the .accdb file is no longer locked.
        OleDbConnection.ReleaseObjectPool();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        try
        {
            Directory.Delete(DataFolder, recursive: true);
        }
        catch (IOException)
        {
            // A locked temp file is harmless; Windows cleans the temp folder.
        }
        GC.SuppressFinalize(this);
    }
}
