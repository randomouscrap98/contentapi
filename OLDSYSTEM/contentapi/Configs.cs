using System;
using System.Collections.Generic;

namespace contentapi.Configs
{
    public class DatabaseConfig
    {
        public string DbType {get;set;}
        public string ConnectionString {get;set;}
        public bool SensitiveLogging {get;set;}
    }

    public class SystemConfig
    {
        public List<long> SuperUsers {get;set;} = new List<long>();
        public bool SuperRootCategories {get;set;}
        public TimeSpan ListenTimeout {get;set;}
        public TimeSpan ListenGracePeriod {get;set;}
    }
}