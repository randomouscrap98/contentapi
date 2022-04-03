namespace contentapi.Main;

/// <summary>
/// All the private data which may be retrieved by users, but ONLY by the user which owns the data.
/// </summary>
public class UserGetPrivateData
{
    public string? email {get;set;}
}

/// <summary>
/// All the private data which may be set by users, but ONLY by the user which owns the data.
/// </summary>
public class UserSetPrivateData : UserGetPrivateData
{
    public string? password {get;set;}
}