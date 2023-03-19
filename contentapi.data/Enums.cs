namespace contentapi.data;

//Make this a bitflag
[Flags]
public enum BanType : long
{
    none = 0,
    @public = 1,    // Ban from public pages only
    @private = 2,   // Ban from private pages only (mix with public for a "full" ban)
    user = 4        // Ban from user related things (self modifications)
    //login = 8       // Ban from login + reject tokens (basically a true 'full ban')
}

//Nobody will see this type most likely
public enum InternalContentType : long
{
    none = 0,
    page = 1,
    module = 2,
    file = 3,
    userpage = 4,
    system = 5
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
    in_group = 1,
    assign_content = 2
}

public enum AdminLogType : long
{
    none = 0,
    group_assign = 1,
    group_create = 2,
    content_create = 3,
    content_update = 4,
    content_delete = 5,
    username_change = 6,
    rethread = 7,
    user_create = 8,
    user_register = 9,
    login_failure = 10,
    user_delete = 11,
    ban_create = 12,
    ban_edit = 13,
    login_temporary = 14,
    login_passwordexpired = 15,
    content_restore = 16,
    userrelation_set = 17,
    userrelation_delete = 18,
    message_delete = 19,
    message_edit = 20
}
