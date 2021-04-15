using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace contentapi.Services.Implementations
{
    public class CacheServiceConfig
    {
        public int MaxCached {get;set;} = 500;
        public int TrimCount {get;set;} = 100;
    }

    public class CacheValue<V>
    {
        private static long nextId = 0;
        public long Id = Interlocked.Increment(ref nextId);
        public V Value;
        public DateTime LastAccess = DateTime.Now;
    }

    public class CacheService<K,V>
    {
        protected readonly object CacheLock = new object();
        protected Dictionary<K, CacheValue<V>> Cache = new Dictionary<K, CacheValue<V>>();

        protected ILogger logger;
        public CacheServiceConfig Config {get;set;}

        public CacheService(ILogger<CacheService<K,V>> logger, CacheServiceConfig config)
        {
            this.logger = logger;
            this.Config = config;
        }

        public void PurgeCache()
        {
            lock(CacheLock)
            {
                logger.LogInformation($"Flushing entire {typeof(V).Name} cache ({Cache.Count} cached)");
                Cache.Clear();
            }
        }

        public void FlushKeys(IEnumerable<K> keys)
        {
            lock(CacheLock)
            {
                logger.LogInformation($"Flushing from {typeof(V).Name} cache keys: {string.Join(",", keys)}");

                foreach(var key in keys)
                    if(Cache.ContainsKey(key))
                        Cache.Remove(key);
            }
        }

        public Dictionary<K,V> GetAll()
        {
            lock(CacheLock)
            {
                return Cache.ToDictionary(x => x.Key, x => x.Value.Value);
            }
        }

        public List<V> GetValues(IEnumerable<K> keys)
        {
            var result = new List<V>();

            lock(CacheLock)
            {
                foreach(var key in keys)
                {
                    if(Cache.ContainsKey(key))
                    {
                        logger.LogDebug($"Using cached {typeof(V).Name} result ({Cache[key].Id}) for key {key}");
                        Cache[key].LastAccess = DateTime.Now;
                        result.Add(Cache[key].Value);
                    }
                }
            }

            return result;
        }

        //Since cache is for speed, easier to just use bool
        public bool GetValue(K key, ref V value)
        {
            var result = GetValues(new[] { key });
            if(result.Count < 1)
                return false;
            value = result[0];
            return true;
        }

        //Store item V in key K, default to NOT overwrite
        public void StoreItem(K key, V value, bool overwrite = false)
        {
            lock(CacheLock)
            {
                if(!Cache.ContainsKey(key))
                {
                    logger.LogDebug($"Caching key ({Cache.Count} keysnow): {key}");
                    Cache.Add(key, new CacheValue<V>() { Value = value });
                }
                else if(overwrite)
                {
                    Cache[key] = new CacheValue<V>() { Value = value };
                }

                if(Cache.Count > Config.MaxCached)
                {
                    logger.LogInformation($"Trimming {typeof(V).Name} cache back {Config.TrimCount} ({Cache.Count} current)");
                    Cache = Cache.OrderBy(x => x.Value.LastAccess).Skip(Config.TrimCount).ToDictionary(x => x.Key, x => x.Value);
                }
            }
        }
    }
}