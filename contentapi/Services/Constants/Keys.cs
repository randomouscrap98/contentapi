using System;
using System.Linq;
using System.Reflection;

namespace contentapi.Services.Constants
{
    public static class Keys
    {
        public const string UserIdentifier = "uid";


        //Symbolic key stuff (these are all prepended to something else)
        public const string AssociatedValueKey = "@";
        public const string VariableKey = "v:";
        public const string HistoryKey = "_";
        public const string ActivityKey = ".";

        //General Relation keys (just relations, no appending)
        //Creator meaning is twofold: entityid1 is the creator of this content and the value is the editor
        public const string CreatorRelation = "rc"; 
        public const string ParentRelation = "rp";
        public const string HistoryRelation = "rh";
        public const string SuperRelation = "rs";
        public const string WatchRelation = "rw";

        public const string VoteRelation = "rv";
        //public const string VoteBadRelation = "rvb";
        //public const string VoteOkRelation = "rvo";
        //public const string VoteGreatRelation = "rvg";

        //public const string UpvoteRelation = "rvu";
        //public const string DownvoteRelation = "rvd";


        //General Value keys
        public const string KeywordKey = "#";


        //Access stuff (I hate that these are individual, hopefully this won't impact performance too bad...)
        //These are also relations
        public const string CreateAction = "!c";
        public const string ReadAction = "!r";
        public const string UpdateAction = "!u";
        public const string DeleteAction = "!d";


        //Overall types of entities (entity types)
        public const string UserType = "tu";
        public const string CategoryType = "tc";
        public const string ContentType = "tp"; //p for page/post
        public const string FileType = "tf";


        //User stuff  (keys for entity values)
        public const string EmailKey = "se";
        public const string PasswordHashKey = "sph";
        public const string PasswordSaltKey = "sps";
        public const string RegistrationCodeKey = "srk";
        public const string AvatarKey = "sa";
        public const string UserSpecialKey = "sus";


        //Awful hacks
        public const string CommentHack = "Zcc";
        public const string CommentHistoryHack ="Zcu";
        public const string CommentDeleteHack ="Zcd";


        //Chaining?
        public const string ChainCommentDelete = "commentdelete";


        public static void EnsureAllUnique()
        {
            var type = typeof(Keys);
            var properties = type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Where(fi => fi.IsLiteral && !fi.IsInitOnly).ToList();
            var values = properties.Select(x => (string)x.GetRawConstantValue());

            if(values.Count() <= 0)
                throw new InvalidOperationException("There are no values!");

            if(values.Distinct().Count() != values.Count())
                throw new InvalidOperationException("There is a duplicate key!");
        }
    }
}