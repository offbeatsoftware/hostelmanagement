using System.Data.OleDb;
using HostelManagement.Data;
using HostelManagement.Utilities;
using Xunit;

namespace HostelManagement.Tests;

public sealed class DatabaseInitializerTests : TestDatabase
{
    [Fact]
    public void Initialize_UsesAnAccessDatabaseEngineProvider()
    {
        Assert.StartsWith("Microsoft.ACE.OLEDB.", Db.ProviderName);
    }

    [Fact]
    public void Initialize_CreatesDatabaseFileInDataFolder()
    {
        Assert.Equal(Path.Combine(DataFolder, "Database", "HostelManagement.accdb"), AppPaths.DatabaseFile);
        Assert.True(File.Exists(AppPaths.DatabaseFile));
    }

    [Fact]
    public void Initialize_CreatesAllTables()
    {
        using OleDbConnection connection = Db.OpenConnection();
        HashSet<string> tables = DatabaseInitializer.GetExistingTableNames(connection);

        foreach (TableDefinition table in DatabaseSchema.Tables)
        {
            Assert.Contains(table.Name, tables);
        }
        Assert.Equal(10, DatabaseSchema.Tables.Count);
    }

    [Fact]
    public void Initialize_CreatesRequiredFolders()
    {
        Assert.True(Directory.Exists(AppPaths.DatabaseFolder));
        Assert.True(Directory.Exists(AppPaths.StudentPhotosFolder));
        Assert.True(Directory.Exists(AppPaths.StudentDocumentsFolder));
        Assert.True(Directory.Exists(AppPaths.BackupsFolder));
        Assert.True(Directory.Exists(AppPaths.LogsFolder));
    }

    [Fact]
    public void Initialize_RunAgain_KeepsExistingData()
    {
        AddStudent("Existing Student");

        DatabaseInitializer.Initialize();

        Assert.Equal(1, Count("Student"));
    }

    [Fact]
    public void Initialize_RecreatesMissingTable()
    {
        Db.Execute("DROP TABLE [EmailHistory]");

        DatabaseInitializer.Initialize();

        using OleDbConnection connection = Db.OpenConnection();
        Assert.Contains("EmailHistory", DatabaseInitializer.GetExistingTableNames(connection));
    }
}
