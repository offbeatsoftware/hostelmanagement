using System.Data.OleDb;
using HostelManagement.Data;
using HostelManagement.Utilities;
using Xunit;

namespace HostelManagement.Tests;

/// <summary>
/// Produces the empty database template (HostelManagement/Database/HostelManagement.accdb).
/// Does nothing unless the HOSTEL_TEMPLATE_OUT environment variable is set; the
/// "Create database template" GitHub workflow sets it and commits the result.
/// </summary>
public sealed class DatabaseTemplateExport
{
    [Fact]
    public void ExportEmptyDatabaseTemplate()
    {
        string? output = Environment.GetEnvironmentVariable("HOSTEL_TEMPLATE_OUT");
        if (string.IsNullOrWhiteSpace(output))
        {
            return;
        }

        string folder = Path.Combine(Path.GetTempPath(), "HostelTemplate", Guid.NewGuid().ToString("N"));
        AppPaths.UseDataFolder(folder);
        AppPaths.EnsureFolders();

        // Build from scratch with ADOX and the schema, never from an older template.
        TestDatabase.CreateFreshDatabase();

        using (OleDbConnection connection = Db.OpenConnection())
        {
            Assert.Equal(DatabaseSchema.Tables.Count,
                DatabaseInitializer.GetExistingTableNames(connection).Count);
        }

        TestDatabase.ReleaseDatabaseFile();

        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        File.Copy(AppPaths.DatabaseFile, output, overwrite: true);
    }
}
