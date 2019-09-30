using System;

namespace contentapi.Configs
{
    public class UsersControllerConfig
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

    public class StartupServiceConfig
    {
        public string SecretKey = null;
        public string ContentConString = null;

        public EmailConfig EmailConfig = null;
    }
}