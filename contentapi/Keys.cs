using System;
using System.Linq;

namespace contentapi
{
    public class Keys
    {
        public string UserIdentifier => "uid";


        //Symbolic key stuff (these are all prepended to something else)
        public string AssociatedValueKey => "@";
        public string VariableKey => "v:";
        public string HistoryKey => "_";
        public string ActivityKey => ".";


        //General Relation keys (just relations, no appending)
        //Creator meaning is twofold: entityid1 is the creator of this content and the value is the editor
        public string CreatorRelation => "rc"; 
        public string ParentRelation => "rp";
        public string HistoryRelation => "rh";
        //public string SuperRelation => "rs";


        //General Value keys
        public string KeywordKey => "#";


        //Access stuff (I hate that these are individual, hopefully this won't impact performance too bad...)
        //These are also relations
        public string CreateAction => "!c";
        public string ReadAction => "!r";
        public string UpdateAction => "!u";
        public string DeleteAction => "!d";


        //Overall types of entities (entity types)
        public string UserType => "tu";
        public string CategoryType => "tc";
        public string ContentType => "tp"; //p for page/post
        public string FileType => "tf";


        //User stuff  (keys for entity values)
        public string EmailKey => "se";
        public string PasswordHashKey => "sph";
        public string PasswordSaltKey => "sps";
        public string RegistrationCodeKey => "srk";
        public string AvatarKey => "sa";


        //Awful hacks
        public string CommentHack => "Zcc";
        public string CommentHistoryHack =>"Zcu";
        public string CommentDeleteHack =>"Zcd";


        public void EnsureAllUnique()
        {
            var properties = GetType().GetProperties();
            var values = properties.Select(x => (string)x.GetValue(this));

            if(values.Distinct().Count() != values.Count())
                throw new InvalidOperationException("There is a duplicate key!");
        }
    }
}