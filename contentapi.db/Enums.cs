namespace contentapi.Db
{
    public enum BanType : long
    {
        none = 0,
        @public = 1
    }

    //Nobody will see this type most likely
    public enum InternalContentType : long
    {
        none = 0,
        page = 1,
        module = 2,
        file = 3
        //category = 4
    }

    public enum UserAction : long
    {
        create = 1,
        read = 2,
        update = 4,
        delete = 8
    }

    public enum UserType : long
    {
        user = 1,
        group = 2
    }

    public enum UserRelationType : long
    {
        inGroup = 1
    }

    public enum AdminLogType : long
    {
        none = 0,
        groupAssign = 1,
        groupRemove = 2,
        contentCreate = 3,
        contentUpdate = 4,
        contentDelete = 5,
        usernameChange = 6
    }

    public enum VoteType : long
    {
        none = 0,
        bad = 1,
        ok = 2,
        good = 3
    }
}
