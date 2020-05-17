using System;

namespace contentapi
{
    public class BadRequestException : Exception
    {
        public BadRequestException() : base() {}
        public BadRequestException(string message) : base(message) { }
    }

    public class NotFoundException : Exception
    {
        public NotFoundException() : base() {}
        public NotFoundException(string message) : base(message) { }
    }

    public class AuthorizationException : Exception
    {
        public AuthorizationException() : base() {}
        public AuthorizationException(string message) : base(message) { }
    }
}