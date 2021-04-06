namespace contentapi
{
    public class Requester
    {
        public long userId {get;set;}
        public bool system {get;set;} = false ;

        public override string ToString()
        {
            return $"{userId}";
        }
    }
}