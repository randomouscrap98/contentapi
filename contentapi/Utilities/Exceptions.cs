namespace contentapi.Utilities;

public class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message) {}
}

public class RequestException : Exception
{
    public RequestException(string message) : base(message) {}
}