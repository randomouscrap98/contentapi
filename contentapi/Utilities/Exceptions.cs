namespace contentapi.Utilities;

public class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message) {}
}

public class RequestException : Exception
{
    public RequestException(string message) : base(message) {}
    public RequestException(string message, Exception inner) : base(message, inner) {}
}

public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) {}
    public NotFoundException(string message, Exception inner) : base(message, inner) {}
}

public class ClosedException : Exception
{
    public ClosedException(string message) : base(message) {}
}

public class RateLimitException : Exception
{
    public RateLimitException(string message) : base(message) {}
    public RateLimitException(string message, Exception inner) : base(message, inner) {}
}

public class TokenException : Exception
{
    public TokenException(string message) : base(message) {}
}