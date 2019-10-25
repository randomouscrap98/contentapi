using System;

namespace contentapi.Configs
{
    public class HashConfig
    {
        public int SaltBits = 256;
        public int HashBits = 512;
        public int HashIterations = 10000;
    }

    public class SessionConfig
    {
        public TimeSpan TokenExpiration = TimeSpan.FromDays(60);
        public string SecretKey = null;
    }

    public class EmailConfig
    {
        public string Host {get;set;}
        public string User {get;set;}
        public string Password {get;set;}
        public int Port {get;set;}

        public string SubjectFront {get;set;}
    }

    public class LanguageConfig
    {
        public string LanguageFolder {get;set;} = null;
        public string DefaultLanguage {get;set;} = "en";
    }

    public class StartupServiceConfig
    {
        public string SecretKey = null;
        public string ContentConString = null;

        //Just use defaults for all these
        public EmailConfig EmailConfig = new EmailConfig();
        public LanguageConfig LanguageConfig = new LanguageConfig();
        public HashConfig HashConfig = new HashConfig();
    }
}