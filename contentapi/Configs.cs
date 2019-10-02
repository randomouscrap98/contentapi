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
        public string Host;
        public string User;
        public string Password;
        public int Port;

        public string SubjectFront;
    }

    public class LanguageConfig
    {
        public string LanguageFolder = null;
        public string DefaultLanguage = "en";
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