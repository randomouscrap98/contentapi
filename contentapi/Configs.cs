using System;
using System.Collections.Generic;
using contentapi.Models;

// These are ALL config objects pulled from appsettings or whatever it's called. Those
// json files that are loaded for development and production.
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

    public class AccessConfig
    {
        //Don't worry about this for now
        public EntityAction OwnerPermissions = EntityAction.Create | EntityAction.Read | EntityAction.Update;

        public Dictionary<EntityAction, string> ActionMapping = new Dictionary<EntityAction, string>()
        {
            { EntityAction.Create, "C" },
            { EntityAction.Read, "R"},
            { EntityAction.Update, "U"},
            { EntityAction.Delete, "D"}
        };
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

    // This is the exception: this config is only used during startup and not
    // pulled from the settings: it is instead an amalgamation.
    public class StartupServiceConfig
    {
        public bool SensitiveDataLogging = false;
        public string SecretKey = null;
        public string ContentConString = null;

        //Just use defaults for all these
        public EmailConfig EmailConfig = new EmailConfig();
        public LanguageConfig LanguageConfig = new LanguageConfig();
        public HashConfig HashConfig = new HashConfig();
        public AccessConfig AccessConfig = new AccessConfig();
    }
}
