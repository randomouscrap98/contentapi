using System;

namespace contentapi.Services
{
    public interface ITempTokenService<T>
    {
        TimeSpan TokenLifetime {get;}

        string GetToken(T id);
        T ValidateToken(string token);
    }
}