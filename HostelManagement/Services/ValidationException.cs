namespace HostelManagement.Services;

/// <summary>
/// Input that breaks a rule (missing field, invalid email, duplicate name).
/// The message is written for the admin and is shown as is.
/// </summary>
public sealed class ValidationException : Exception
{
    public ValidationException(string message)
        : base(message)
    {
    }
}
