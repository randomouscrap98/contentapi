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
        in_group = 1
    }

    public enum AdminLogType : long
    {
        none = 0,
        group_assign = 1,
        group_remove = 2,
        content_create = 3,
        content_update = 4,
        content_delete = 5,
        username_change = 6,
        rethread = 7,
        user_create = 8,
        user_register = 9,
        login_failure = 10
    }

    public enum VoteType : long
    {
        none = 0,
        bad = 1,
        ok = 2,
        good = 3
    }
}
