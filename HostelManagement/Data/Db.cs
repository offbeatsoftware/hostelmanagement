using System.Data;
using System.Data.OleDb;

namespace HostelManagement.Data;

/// <summary>
/// Reusable data access helper for the Access database.
///
/// Rules for all SQL in this application:
/// <list type="bullet">
/// <item>Never concatenate user input into SQL. Use <c>?</c> placeholders and <see cref="Param"/>.</item>
/// <item>OLE DB parameters are positional: pass them in the same order as the <c>?</c> placeholders.</item>
/// <item>Wrap table and column names in [brackets] to avoid clashes with Access reserved words.</item>
/// </list>
/// </summary>
public static class Db
{
    private const string NotOpenedMessage =
        "The database could not be opened.\n\n" +
        "Please close any other program that is using the database (for example Microsoft Access) and try again.";

    private static string? _connectionString;

    /// <summary>The OLE DB provider in use, for example Microsoft.ACE.OLEDB.16.0.</summary>
    public static string ProviderName { get; private set; } = string.Empty;

    public static bool IsConfigured => _connectionString is not null;

    internal static void Configure(string providerName, string databaseFile)
    {
        ProviderName = providerName;
        _connectionString = BuildConnectionString(providerName, databaseFile);
    }

    // Keep this to Provider and Data Source only: ADOX refuses to create a database
    // when other settings such as "Persist Security Info" are present.
    internal static string BuildConnectionString(string providerName, string databaseFile) =>
        new OleDbConnectionStringBuilder
        {
            Provider = providerName,
            DataSource = databaseFile,
        }.ConnectionString;

    /// <summary>Opens a new connection. Always dispose it with <c>using</c>.</summary>
    public static OleDbConnection OpenConnection()
    {
        if (_connectionString is null)
        {
            throw new InvalidOperationException("The database has not been initialized.");
        }

        var connection = new OleDbConnection(_connectionString);
        try
        {
            connection.Open();
            return connection;
        }
        catch (Exception ex) when (ex is OleDbException or InvalidOperationException)
        {
            connection.Dispose();
            throw new DatabaseException(NotOpenedMessage, ex);
        }
    }

    /// <summary>Creates a positional parameter with the correct Access data type for the value.</summary>
    /// <param name="name">Descriptive name only (OLE DB ignores it); makes the SQL easier to read.</param>
    public static OleDbParameter Param(string name, object? value)
    {
        var parameter = new OleDbParameter { ParameterName = name };

        switch (value)
        {
            case null:
            case DBNull:
                parameter.OleDbType = OleDbType.VarWChar;
                parameter.Value = DBNull.Value;
                break;
            case string text:
                parameter.OleDbType = text.Length > 255 ? OleDbType.LongVarWChar : OleDbType.VarWChar;
                parameter.Value = text;
                break;
            case int number:
                parameter.OleDbType = OleDbType.Integer;
                parameter.Value = number;
                break;
            case decimal amount:
                parameter.OleDbType = OleDbType.Currency;
                parameter.Value = amount;
                break;
            case double number:
                parameter.OleDbType = OleDbType.Double;
                parameter.Value = number;
                break;
            case bool flag:
                parameter.OleDbType = OleDbType.Boolean;
                parameter.Value = flag;
                break;
            case DateTime date:
                // OleDbType.Date avoids the "Data type mismatch" error Access raises for DBTimeStamp.
                parameter.OleDbType = OleDbType.Date;
                parameter.Value = date;
                break;
            default:
                throw new ArgumentException($"Unsupported parameter type {value.GetType().Name} for {name}.");
        }

        return parameter;
    }

    /// <summary>Text parameter that stores an empty value as NULL (for optional fields).</summary>
    public static OleDbParameter OptionalText(string name, string value) =>
        Param(name, string.IsNullOrEmpty(value) ? null : value);

    // ---- Single statement helpers (open and close their own connection) ----

    /// <summary>Runs an INSERT, UPDATE or DELETE and returns the number of affected rows.</summary>
    public static int Execute(string sql, params OleDbParameter[] parameters)
    {
        using OleDbConnection connection = OpenConnection();
        return Execute(connection, null, sql, parameters);
    }

    public static object? Scalar(string sql, params OleDbParameter[] parameters)
    {
        using OleDbConnection connection = OpenConnection();
        return Scalar(connection, null, sql, parameters);
    }

    public static List<T> Query<T>(string sql, Func<IDataRecord, T> map, params OleDbParameter[] parameters)
    {
        using OleDbConnection connection = OpenConnection();
        return Query(connection, null, sql, map, parameters);
    }

    /// <summary>Runs an INSERT and returns the new AutoNumber id.</summary>
    public static int Insert(string sql, params OleDbParameter[] parameters)
    {
        using OleDbConnection connection = OpenConnection();
        return Insert(connection, null, sql, parameters);
    }

    // ---- Helpers for use inside a transaction or an existing connection ----

    public static int Execute(OleDbConnection connection, OleDbTransaction? transaction, string sql,
        params OleDbParameter[] parameters)
    {
        using OleDbCommand command = CreateCommand(connection, transaction, sql, parameters);
        return command.ExecuteNonQuery();
    }

    public static object? Scalar(OleDbConnection connection, OleDbTransaction? transaction, string sql,
        params OleDbParameter[] parameters)
    {
        using OleDbCommand command = CreateCommand(connection, transaction, sql, parameters);
        object? result = command.ExecuteScalar();
        return result is DBNull ? null : result;
    }

    public static List<T> Query<T>(OleDbConnection connection, OleDbTransaction? transaction, string sql,
        Func<IDataRecord, T> map, params OleDbParameter[] parameters)
    {
        using OleDbCommand command = CreateCommand(connection, transaction, sql, parameters);
        using OleDbDataReader reader = command.ExecuteReader();

        var results = new List<T>();
        while (reader.Read())
        {
            results.Add(map(reader));
        }
        return results;
    }

    public static int Insert(OleDbConnection connection, OleDbTransaction? transaction, string sql,
        params OleDbParameter[] parameters)
    {
        Execute(connection, transaction, sql, parameters);
        // @@IDENTITY is per connection in Access, so it returns the id generated by the INSERT above.
        return Convert.ToInt32(Scalar(connection, transaction, "SELECT @@IDENTITY"));
    }

    /// <summary>
    /// Runs several statements as one unit: either all of them are saved or none are.
    /// Use for multi-step actions such as room transfer or invoice + invoice items.
    /// </summary>
    public static T InTransaction<T>(Func<OleDbConnection, OleDbTransaction, T> work)
    {
        using OleDbConnection connection = OpenConnection();
        using OleDbTransaction transaction = connection.BeginTransaction();
        try
        {
            T result = work(connection, transaction);
            transaction.Commit();
            return result;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public static void InTransaction(Action<OleDbConnection, OleDbTransaction> work) =>
        InTransaction<object?>((connection, transaction) =>
        {
            work(connection, transaction);
            return null;
        });

    // Access error 3022: "...would create duplicate values in the index, primary key, or relationship."
    // The ACE provider often reports it only in the exception message, with an empty Errors list.
    private const int AccessDuplicateKeyNativeError = -105121349;

    /// <summary>True when Access rejected a value because a unique index already contains it.</summary>
    public static bool IsDuplicateKeyError(OleDbException ex) =>
        IsDuplicateKeyMessage(ex.Message) ||
        ex.Errors.Cast<OleDbError>().Any(error =>
            error.SQLState == "3022" ||
            error.NativeError == AccessDuplicateKeyNativeError ||
            IsDuplicateKeyMessage(error.Message));

    private static bool IsDuplicateKeyMessage(string? message) =>
        message?.Contains("duplicate values", StringComparison.OrdinalIgnoreCase) == true;

    private static OleDbCommand CreateCommand(OleDbConnection connection, OleDbTransaction? transaction,
        string sql, OleDbParameter[] parameters)
    {
        var command = new OleDbCommand(sql, connection, transaction);
        command.Parameters.AddRange(parameters);
        return command;
    }
}
