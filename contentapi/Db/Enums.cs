namespace contentapi.Db;

public enum BanType
{
    none = 0,
    @public = 1
}

//Nobody will see this type most likely
public enum InternalContentType
{
    none = 0,
    page = 1,
    module = 2,
    file = 3
    //category = 4
}

public enum UserAction
{
    create = 1,
    read = 2,
    update = 4,
    delete = 8
}

public enum UserType
{
    user = 0,
    group = 1
}

public enum UserRelationType
{
    inGroup = 0
}

public enum AdminLogType
{
    none = 0,
    groupAssign = 1,
    groupRemove = 2,
    contentCreate = 3,
    contentUpdate = 4,
    contentDelete = 5
}

public enum VoteType
{
    none = 0,
    bad = 1,
    ok = 2,
    good = 3
}
