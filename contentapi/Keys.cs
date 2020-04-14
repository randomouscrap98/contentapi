using System;
using System.Linq;

namespace contentapi
{
    public class Keys
    {
        public string UserIdentifier => "uid";
        public string ActiveIdentifier => "a";

        //Overall types of entities
        public string TypeUser => "tu";
        public string TypeCategory => "tc";
        public string TypeContent => "tp"; //p for page/post
        public string TypeComment => "ta"; //a for addendum or annotation
        public string TypeStandIn => "ts";

        //Historic keys
        public string HistoryKey => "_";
        public string StandInRelation => ">";

        //User stuff 
        public string EmailKey => "se";
        public string PasswordHashKey => "sph";
        public string PasswordSaltKey => "sps";
        public string RegistrationCodeKey => "srk";

        //Category stuff

        public void EnsureAllUnique()
        {
            var properties = GetType().GetProperties();
            var values = properties.Select(x => (string)x.GetValue(this));

            if(values.Distinct().Count() != values.Count())
                throw new InvalidOperationException("There is a duplicate key!");
        }
    }
}