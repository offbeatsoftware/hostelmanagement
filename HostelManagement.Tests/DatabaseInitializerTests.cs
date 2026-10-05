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
    public void CreateDatabaseFile_WithoutTemplate_CreatesEmptyAccessFile()
    {
        ReleaseDatabaseFile();
        File.Delete(AppPaths.DatabaseFile);

        DatabaseInitializer.CreateDatabaseFile(Db.ProviderName);
        DatabaseInitializer.Initialize();

        Assert.True(File.Exists(AppPaths.DatabaseFile));
        Assert.Equal(0, Count("Student"));
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
        Assert.Equal(12, DatabaseSchema.Tables.Count);
    }

    [Fact]
    public void Initialize_StoresCurrentSchemaVersion()
    {
        Assert.Equal(DatabaseSchema.Version, Convert.ToInt32(Db.Scalar("SELECT MAX([Version]) FROM [SchemaInfo]")));
        Assert.Equal(1, Count("SchemaInfo"));

        DatabaseInitializer.Initialize();

        Assert.Equal(1, Count("SchemaInfo"));
    }

    [Fact]
    public void Initialize_DatabaseFromBeforeVersioning_IsRejectedWithFriendlyMessage()
    {
        Db.Execute("DROP TABLE [SchemaInfo]");

        var ex = Assert.Throws<DatabaseException>(DatabaseInitializer.Initialize);

        Assert.Contains("earlier version", ex.Message);
        Assert.Contains(AppPaths.DatabaseFile, ex.Message);
    }

    [Fact]
    public void Initialize_OlderSchemaVersion_IsRejected()
    {
        Db.Execute("UPDATE [SchemaInfo] SET [Version] = ?", Db.Param("@Version", DatabaseSchema.Version - 1));

        var ex = Assert.Throws<DatabaseException>(DatabaseInitializer.Initialize);

        Assert.Contains("earlier version", ex.Message);
    }

    [Fact]
    public void Initialize_NewerSchemaVersion_IsRejected()
    {
        Db.Execute("UPDATE [SchemaInfo] SET [Version] = ?", Db.Param("@Version", DatabaseSchema.Version + 1));

        var ex = Assert.Throws<DatabaseException>(DatabaseInitializer.Initialize);

        Assert.Contains("newer version", ex.Message);
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
