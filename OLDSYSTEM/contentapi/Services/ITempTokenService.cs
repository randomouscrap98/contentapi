using System;

namespace contentapi.Services
{
    public interface ITempTokenService<T>
    {
        TimeSpan TokenLifetime {get;}

        int InvalidateTokens(T id);
        string GetToken(T id);
        T ValidateToken(string token);
    }

    //public interface IUserTempTokenService : ITempTokenService<long>{}
}