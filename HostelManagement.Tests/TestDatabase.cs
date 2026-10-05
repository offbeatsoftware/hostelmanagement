using System.Data.OleDb;
using HostelManagement.Data;
using HostelManagement.Models;
using HostelManagement.Services;
using HostelManagement.Utilities;

namespace HostelManagement.Tests;

/// <summary>
/// Base class for tests that need a database. Each test gets a brand new database
/// in its own temporary folder, so the application's own database is never touched.
/// </summary>
public abstract class TestDatabase : IDisposable
{
    protected TestDatabase()
    {
        DataFolder = Path.Combine(Path.GetTempPath(), "HostelManagementTests", Guid.NewGuid().ToString("N"));
        AppPaths.UseDataFolder(DataFolder);
        AppPaths.EnsureFolders();
        CreateFreshDatabase();
    }

    /// <summary>Builds the database from the schema with ADOX, ignoring any shipped template.</summary>
    internal static void CreateFreshDatabase()
    {
        DatabaseInitializer.ConfigureProvider();
        DatabaseInitializer.CreateDatabaseFile(Db.ProviderName);
        DatabaseInitializer.Initialize();
    }

    protected string DataFolder { get; }

    protected static int Count(string table) =>
        Convert.ToInt32(Db.Scalar($"SELECT COUNT(*) FROM [{table}]"));

    private int? _hostelId;
    private int? _collegeId;

    /// <summary>A hostel created for the test on first use.</summary>
    protected int HostelId => _hostelId ??= AddHostel("Test Hostel");

    /// <summary>A college of <see cref="HostelId"/> created on first use.</summary>
    protected int CollegeId => _collegeId ??= AddCollege(HostelId, "Test College");

    protected static int AddHostel(string name) =>
        HostelService.Save(new Hostel { HostelName = name }).HostelId;

    protected static int AddCollege(int hostelId, string name) =>
        CollegeService.Save(new College { HostelId = hostelId, CollegeName = name }).CollegeId;

    protected int AddStudent(string name = "Test Student", int? collegeId = null) =>
        Db.Insert("INSERT INTO [Student] ([StudentName], [CollegeId], [Status]) VALUES (?, ?, ?)",
            Db.Param("@StudentName", name),
            Db.Param("@CollegeId", collegeId ?? CollegeId),
            Db.Param("@Status", "Active"));

    /// <summary>The id of the Single (1), Double (2) or Triple (3) sharing type of a hostel (default: <see cref="HostelId"/>).</summary>
    protected int SharingTypeId(int capacity, int? hostelId = null) =>
        Convert.ToInt32(Db.Scalar("SELECT [SharingTypeId] FROM [SharingType] WHERE [HostelId] = ? AND [Capacity] = ?",
            Db.Param("@HostelId", hostelId ?? HostelId),
            Db.Param("@Capacity", capacity)));

    /// <summary>Records a student in a room directly (Room Allocation is built in a later phase).</summary>
    protected void AddAllocation(int roomId, string status = AllocationStatus.Current)
    {
        int studentId = AddStudent($"Student {Guid.NewGuid():N}"[..20]);
        Db.Execute(
            "INSERT INTO [RoomAllocation] ([StudentId], [RoomId], [CheckInDate], [Status]) VALUES (?, ?, ?, ?)",
            Db.Param("@StudentId", studentId),
            Db.Param("@RoomId", roomId),
            Db.Param("@CheckInDate", DateTime.Today),
            Db.Param("@Status", status));
    }

    /// <summary>Releases pooled connections so the .accdb file is no longer locked.</summary>
    internal static void ReleaseDatabaseFile()
    {
        OleDbConnection.ReleaseObjectPool();
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }

    public void Dispose()
    {
        ReleaseDatabaseFile();
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
