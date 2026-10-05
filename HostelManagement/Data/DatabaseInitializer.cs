using System.Data;
using System.Data.OleDb;
using System.Reflection;
using System.Runtime.InteropServices;
using HostelManagement.Utilities;

namespace HostelManagement.Data;

/// <summary>
/// Runs at startup: finds the Access Database Engine, creates the database file
/// if it does not exist, and creates any missing tables. Safe to run on every start.
/// </summary>
public static class DatabaseInitializer
{
    // Newest first. The 2016 engine and Microsoft 365 register 16.0; older installs register 12.0.
    private static readonly string[] SupportedProviders =
    [
        "Microsoft.ACE.OLEDB.16.0",
        "Microsoft.ACE.OLEDB.12.0",
    ];

    private const string EngineMissingMessage =
        "The Microsoft Access Database Engine was not found on this computer.\n\n" +
        "Please install the \"Microsoft Access Database Engine 2016 Redistributable\" (64 bit version, " +
        "file accessdatabaseengine_X64.exe) from Microsoft and start the application again.";

    private static string CreateFailedMessage =>
        $"The database file could not be created in {AppPaths.DataFolder}.\n\n" +
        "Please check that the folder exists and is not read only, then start the application again.";

    // Shown while the application is in development and no real data exists yet.
    private static string OutdatedMessage =>
        "This database was created by an earlier version of the application and its layout has changed.\n\n" +
        $"Close the application, delete the file {AppPaths.DatabaseFile} and start the application again. " +
        "A new, empty database will be created.";

    private const string SetupFailedMessage =
        "The database tables could not be prepared.\n\n" +
        "Please close any other program that is using the database (for example Microsoft Access) " +
        "and start the application again.";

    public static void Initialize()
    {
        string provider = ConfigureProvider();

        // The build places the empty database from the repository here; create one if it is missing.
        if (!File.Exists(AppPaths.DatabaseFile))
        {
            CreateDatabaseFile(provider);
            AppLogger.Info($"Created database {AppPaths.DatabaseFile}.");
        }

        CreateMissingTables();
    }

    /// <summary>Finds the installed Access Database Engine and points <see cref="Db"/> at the database file.</summary>
    internal static string ConfigureProvider()
    {
        string provider = FindInstalledProvider() ?? throw new DatabaseException(EngineMissingMessage);
        Db.Configure(provider, AppPaths.DatabaseFile);
        AppLogger.Info($"Using OLE DB provider {provider}.");
        return provider;
    }

    /// <summary>Names of the application tables that exist in the database.</summary>
    public static HashSet<string> GetExistingTableNames(OleDbConnection connection)
    {
        DataTable? tables = connection.GetOleDbSchemaTable(
            OleDbSchemaGuid.Tables, [null, null, null, "TABLE"]);

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (tables is not null)
        {
            foreach (DataRow row in tables.Rows)
            {
                names.Add((string)row["TABLE_NAME"]);
            }
        }
        return names;
    }

    private static string? FindInstalledProvider()
    {
        try
        {
            using DataTable providers = new OleDbEnumerator().GetElements();
            var installed = providers.Rows.Cast<DataRow>()
                .Select(row => row["SOURCES_NAME"] as string)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return SupportedProviders.FirstOrDefault(installed.Contains);
        }
        catch (Exception ex)
        {
            AppLogger.Error("Could not list the installed OLE DB providers.", ex);
            return null;
        }
    }

    /// <summary>
    /// Creates an empty .accdb file with ADOX, which is part of Windows, so Microsoft Access
    /// itself does not need to be installed.
    /// </summary>
    internal static void CreateDatabaseFile(string provider)
    {
        // The .accdb extension makes the ACE provider create an Access 2007+ format file.
        string connectionString = Db.BuildConnectionString(provider, AppPaths.DatabaseFile);

        object? catalog = null;
        object? connection = null;
        try
        {
            Type catalogType = Type.GetTypeFromProgID("ADOX.Catalog")
                ?? throw new InvalidOperationException("ADOX.Catalog is not registered.");

            catalog = Activator.CreateInstance(catalogType)!;
            connection = catalogType.InvokeMember(
                "Create", BindingFlags.InvokeMethod, null, catalog, [connectionString]);

            connection?.GetType().InvokeMember("Close", BindingFlags.InvokeMethod, null, connection, null);
        }
        catch (Exception ex)
        {
            TryDeletePartialFile();
            throw new DatabaseException(CreateFailedMessage, ex);
        }
        finally
        {
            if (connection is not null && Marshal.IsComObject(connection))
            {
                Marshal.FinalReleaseComObject(connection);
            }
            if (catalog is not null && Marshal.IsComObject(catalog))
            {
                Marshal.FinalReleaseComObject(catalog);
            }
        }
    }

    private static void CreateMissingTables()
    {
        try
        {
            using OleDbConnection connection = Db.OpenConnection();
            HashSet<string> existing = GetExistingTableNames(connection);

            // A database that already has tables but no SchemaInfo was built before versioning.
            bool isEmptyDatabase = existing.Count == 0;
            if (!isEmptyDatabase && !existing.Contains("SchemaInfo"))
            {
                throw new DatabaseException(OutdatedMessage);
            }

            foreach (TableDefinition table in DatabaseSchema.Tables)
            {
                if (!existing.Contains(table.Name))
                {
                    CreateTable(connection, table);
                    AppLogger.Info($"Created table {table.Name}.");
                }
            }

            CheckSchemaVersion(connection);
            AddDefaultSharingTypes(connection);
        }
        catch (DatabaseException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new DatabaseException(SetupFailedMessage, ex);
        }
    }

    private static void AddDefaultSharingTypes(OleDbConnection connection)
    {
        if (Convert.ToInt32(Db.Scalar(connection, null, "SELECT COUNT(*) FROM [SharingType]")) > 0)
        {
            return;
        }

        foreach ((string name, int capacity) in DatabaseSchema.DefaultSharingTypes)
        {
            Db.Execute(connection, null,
                "INSERT INTO [SharingType] ([SharingName], [Capacity], [Rent]) VALUES (?, ?, ?)",
                Db.Param("@SharingName", name),
                Db.Param("@Capacity", capacity),
                Db.Param("@Rent", 0m));
        }
        AppLogger.Info("Added the default sharing types.");
    }

    private static void CheckSchemaVersion(OleDbConnection connection)
    {
        object? stored = Db.Scalar(connection, null, "SELECT MAX([Version]) FROM [SchemaInfo]");
        if (stored is null)
        {
            Db.Execute(connection, null, "INSERT INTO [SchemaInfo] ([Version]) VALUES (?)",
                Db.Param("@Version", DatabaseSchema.Version));
            return;
        }

        int version = Convert.ToInt32(stored);
        if (version < DatabaseSchema.Version)
        {
            throw new DatabaseException(OutdatedMessage);
        }
        if (version > DatabaseSchema.Version)
        {
            throw new DatabaseException(
                "This database was created by a newer version of the application.\n\n" +
                "Please install the latest version of the application.");
        }
    }

    private static void CreateTable(OleDbConnection connection, TableDefinition table)
    {
        try
        {
            foreach (string statement in table.Statements)
            {
                Db.Execute(connection, null, statement);
            }
        }
        catch
        {
            // Remove a half created table so the next start creates it again cleanly.
            try
            {
                Db.Execute(connection, null, $"DROP TABLE [{table.Name}]");
            }
            catch (OleDbException)
            {
                // The CREATE TABLE itself failed, so there is nothing to drop.
            }
            throw;
        }
    }

    private static void TryDeletePartialFile()
    {
        try
        {
            if (File.Exists(AppPaths.DatabaseFile))
            {
                File.Delete(AppPaths.DatabaseFile);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
