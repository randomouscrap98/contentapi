using System;
using System.Linq;

namespace contentapi
{
    public class Keys
    {
        public string UserIdentifier => "uid";


        //Symbolic value stuff (these are prepended to actual values)
        public string ActiveValue => "+";
        public string InactiveValue => "-";


        //Symbolic key stuff (these are all prepended to something else)
        public string AssociatedValueKey => "@";
        public string VariableKey => "v:";
        public string HistoryKey => "_";


        //General Relation keys (just relations, no appending)
        public string CreatorRelation => "rc";
        public string ParentRelation => "rp";
        public string StandInRelation => ">";


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
        public string CommentType => "ta"; //a for addendum or annotation
        public string StandInType => "ts";


        //User stuff  (keys for entity values)
        public string EmailKey => "se";
        public string PasswordHashKey => "sph";
        public string PasswordSaltKey => "sps";
        public string RegistrationCodeKey => "srk";


        public void EnsureAllUnique()
        {
            var properties = GetType().GetProperties();
            var values = properties.Select(x => (string)x.GetValue(this));

            if(values.Distinct().Count() != values.Count())
                throw new InvalidOperationException("There is a duplicate key!");
        }
    }
}