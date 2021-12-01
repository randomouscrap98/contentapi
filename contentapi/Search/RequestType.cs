namespace contentapi.Search;

public enum RequestType
{
    user,
    content,
    comment,
    page,
    file,
    module,
    activity,
    watch,
    adminlog,
    agent, //This is 'all' users (including groups/etc), useful for permissions 
    group
}