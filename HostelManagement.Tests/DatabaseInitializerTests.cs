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
        Assert.Equal(14, DatabaseSchema.Tables.Count);
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
        AddHostel("Boys Hostel");
        Db.Execute("DROP TABLE [SchemaInfo]");

        var ex = Assert.Throws<DatabaseException>(DatabaseInitializer.Initialize);

        Assert.Contains("earlier version", ex.Message);
        Assert.Contains(AppPaths.DatabaseFile, ex.Message);
    }

    [Fact]
    public void Initialize_OlderSchemaVersionWithData_IsRejected()
    {
        AddHostel("Boys Hostel");
        Db.Execute("UPDATE [SchemaInfo] SET [Version] = ?", Db.Param("@Version", DatabaseSchema.FirstUpgradableVersion - 1));

        var ex = Assert.Throws<DatabaseException>(DatabaseInitializer.Initialize);

        Assert.Contains("earlier version", ex.Message);
    }

    [Fact]
    public void Initialize_EmptyDatabaseOfAnOlderVersion_IsReplaced()
    {
        Db.Execute("UPDATE [SchemaInfo] SET [Version] = ?", Db.Param("@Version", DatabaseSchema.FirstUpgradableVersion - 1));
        Db.Execute("ALTER TABLE [RoomAllocation] DROP COLUMN [BedNumber]");

        DatabaseInitializer.Initialize();

        Assert.Equal(DatabaseSchema.Version, Convert.ToInt32(Db.Scalar("SELECT MAX([Version]) FROM [SchemaInfo]")));
        Assert.Equal(0, Convert.ToInt32(Db.Scalar("SELECT COUNT([BedNumber]) FROM [RoomAllocation]")));
        Assert.True(Services.AuthService.IsValidLogin("admin", "admin"));
    }

    [Fact]
    public void Initialize_EmptyOlderVersion_IsReplaced_KeepingTheLoginAndSettings_ButNotTheOldFeeTexts()
    {
        Services.AuthService.IsValidLogin("admin", "admin");
        Services.AuthService.ChangePassword("admin", "Balaji@2026", "Balaji@2026");
        Services.AuthService.SaveContact("owner@gmail.com", "98290 12345");
        AppSettingRepository.SaveAll(new Dictionary<string, string>
        {
            ["Email.SenderEmail"] = "hostel@gmail.com",
            ["Email.Absence.Subject"] = "Absent: {StudentName}",
            ["Email.Reminder.Subject"] = "Overdue {Overdue}",
        });
        Db.Execute("UPDATE [SchemaInfo] SET [Version] = ?", Db.Param("@Version", DatabaseSchema.FirstUpgradableVersion - 1));
        Db.Execute("ALTER TABLE [Student] DROP COLUMN [MotherEmail]");

        DatabaseInitializer.Initialize();

        Assert.Equal(DatabaseSchema.Version, Convert.ToInt32(Db.Scalar("SELECT MAX([Version]) FROM [SchemaInfo]")));
        Assert.Equal(0, Convert.ToInt32(Db.Scalar("SELECT COUNT([MotherEmail]) FROM [Student]")));
        Assert.True(Services.AuthService.IsValidLogin("admin", "Balaji@2026"));
        Assert.False(Services.AuthService.IsValidLogin("admin", "admin"));
        Assert.Equal("owner@gmail.com", Services.AuthService.GetAdminEmail());
        Models.EmailSettings settings = Services.EmailSettingsService.Get();
        Assert.Equal("hostel@gmail.com", settings.SenderEmail);
        Assert.Equal("Absent: {StudentName}", settings.Absence.Subject);
        Assert.Equal(Models.EmailSettings.DefaultReminder, settings.Reminder);
    }

    [Fact]
    public void Initialize_DatabaseOfVersion1_2_IsUpgradedInPlace_KeepingTheData()
    {
        AddStudent("Existing Student");
        Services.AuthService.IsValidLogin("admin", "admin");
        Services.AuthService.ChangePassword("admin", "Balaji@2026", "Balaji@2026");
        Db.Execute("DROP TABLE [PaymentChange]");
        Db.Execute("UPDATE [SchemaInfo] SET [Version] = ?", Db.Param("@Version", DatabaseSchema.FirstUpgradableVersion));

        DatabaseInitializer.Initialize();

        Assert.Equal(DatabaseSchema.Version, Convert.ToInt32(Db.Scalar("SELECT MAX([Version]) FROM [SchemaInfo]")));
        Assert.Equal(1, Count("Student"));
        Assert.Equal(0, Count("PaymentChange"));
        Assert.True(Services.AuthService.IsValidLogin("admin", "Balaji@2026"));
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
