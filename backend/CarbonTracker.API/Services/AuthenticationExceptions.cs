namespace CarbonTracker.API.Services;

public sealed class DuplicateAccountException : Exception
{
    public DuplicateAccountException() : base("Username or email already exists.") { }
}

public sealed class InvalidResetTokenException : Exception
{
    public InvalidResetTokenException() : base("Invalid or expired token.") { }
}
