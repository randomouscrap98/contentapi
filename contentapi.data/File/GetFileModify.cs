namespace contentapi.data;

public class GetFileModify
{
    public int size { get; set; }
    public int maxSize { get; set; }
    public bool crop { get; set; }
    public bool freeze { get; set; } = false;
}
