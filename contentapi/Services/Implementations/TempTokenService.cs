using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace contentapi.Services.Implementations
{
    public class TempTokenServiceConfig
    {
        public TimeSpan TokenLifetime {get;set;}
    }

    public class TempTokenService<T> : ITempTokenService<T>
    {
        protected class TokenData
        {
            public string token = Guid.NewGuid().ToString();
            public DateTime created = DateTime.Now;
        }

        protected ILogger<TempTokenService<T>> logger;
        protected TempTokenServiceConfig config;

        protected Dictionary<T, TokenData> tokens = new Dictionary<T, TokenData>();
        protected readonly object tokenLock = new object();

        public TempTokenService(ILogger<TempTokenService<T>> logger, IOptionsMonitor<TempTokenServiceConfig> config)
        {
            this.logger = logger;
            this.config = config.CurrentValue;
        }

        public TimeSpan TokenLifetime => config.TokenLifetime;

        protected void CleanDictionary()
        {
            lock(tokenLock)
            {
                var removals = tokens.Where(x => (DateTime.Now - x.Value.created) > TokenLifetime); 

                foreach(var removal in removals)
                    tokens.Remove(removal.Key);
            }
        }

        public string GetToken(T id)
        {
            CleanDictionary();

            lock(tokenLock)
            {
                if(!tokens.ContainsKey(id))
                    tokens.Add(id, new TokenData());

                return tokens[id].token;
            }
        }

        public T ValidateToken(string token)
        {
            CleanDictionary();

            lock(tokenLock)
            {
                try
                {
                    return tokens.First(x => x.Value.token == token).Key;
                }
                catch
                {
                    throw new InvalidOperationException("Invalid token");
                }
            }
        }
    }
}