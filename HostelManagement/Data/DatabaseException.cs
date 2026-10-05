namespace HostelManagement.Data;

/// <summary>
/// A database problem with a message that is safe to show to the admin.
/// The technical cause is kept in <see cref="Exception.InnerException"/> for the log.
/// </summary>
public sealed class DatabaseException : Exception
{
    public DatabaseException(string userMessage, Exception? innerException = null)
        : base(userMessage, innerException)
    {
    }
}
