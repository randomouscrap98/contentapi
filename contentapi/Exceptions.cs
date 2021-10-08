using System;

namespace contentapi
{
    public class BadRequestException : Exception
    {
        public BadRequestException() : base() {}
        public BadRequestException(string message) : base(message) { }
        public BadRequestException(string message, Exception ex) : base(message, ex) { }
    }

    public class NotFoundException : Exception
    {
        public NotFoundException() : base() {}
        public NotFoundException(string message) : base(message) { }
    }

    /*public class AuthorizationException : Exception
    {
        public AuthorizationException() : base() {}
        public AuthorizationException(string message) : base(message) { }
    }*/

    public class ForbiddenException : Exception
    {
        public ForbiddenException() : base() {}
        public ForbiddenException(string message) : base(message) {}
    }

    public class BannedException : Exception
    {
        public BannedException() : base() {}
        public BannedException(string message) : base(message) {}
    }
}