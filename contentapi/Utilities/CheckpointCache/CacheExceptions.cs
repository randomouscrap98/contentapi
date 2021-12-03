namespace contentapi.Utilities;

public class ExpiredCheckpointException : Exception
{
    public ExpiredCheckpointException(string message) : base(message) {}
}