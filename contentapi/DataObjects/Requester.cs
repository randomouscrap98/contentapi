namespace contentapi
{
    public class Requester
    {
        public long userId;

        public bool system = false;

        public override string ToString()
        {
            return $"{userId}";
        }
    }
}