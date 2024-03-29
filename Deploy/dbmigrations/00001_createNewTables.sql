create table if not exists users (
    id integer primary key,
    username text not null,
    avatar text not null default '0',
    createDate text not null,
    special text,
    -- need bans, there's a bool banned in users
    -- also need to mark supers, they're pulled from a file for some reason.
    -- maybe change that?
    super int not null default 0,
    deleted int not null default 0,
    type int not null default 0,  --The type of user, can specify that it's a group
    editDate text,
    -- user view also has ban object, represents current ban
    email text not null,
    hidelist text,
    password text not null,
    salt text not null,
    registrationKey text
);

-- Relates users to other things. Useful for groups, may be used for other things.
create table if not exists user_relations (
    id integer primary key,
    type integer not null,
    createDate text not null,
    userId integer not null,
    relatedId integer not null
);

create index if not exists idx_user_relations_typeuserid on user_relations(type, userId);

-- A simple way to keep a human-readable log of many important actions, such as group assigns, bans, etc.
create table if not exists admin_log (
    id integer primary key,
    type integer not null, -- The type of action; some things can be "none" for simple messages
    `text` text,
    createDate text not null,
    initiator integer not null,
    target integer not null
);

create index if not exists idx_admin_log_typedate on admin_log(type, createDate);

-- To simplify, we're not letting content values be edited. as such, the
-- content's modify date will become the value date
create table if not exists user_variables(
    id integer primary key,
    userId int not null,
    createDate text not null,
    editDate text,
    editCount int not null default 0,
    `key` text not null,
    `value` text
);

-- This COULD become a performance concern if people keep updating their user variables...
-- but tbf it's just the key so...
create index if not exists idx_user_variables_userId on user_variables(userId, key);

create table if not exists bans (
    id integer primary key,
    createDate text not null,
    expireDate text not null,
    createUserId int not null,
    bannedUserId int not null,
    message text,
    type int not null  -- all enums should be stored as integers for performance
);

-- Note: Only create indexes if you NEED them!

create table if not exists content (
    id integer primary key,
    deleted int not null default 0, -- deleting removes the id, don't want that.
    createUserId int not null,
    createDate text not null,
    contentType int not null,   --page, file, module, category etc (use an enum)
    literalType text default null,  --the actual type of the content represented by this, like a file mimetype
    `meta` text default null,  
    `description` text default null,  
    `hash` text not null,  
    `name` text not null,
    `text` text not null,
    parentId int not null default 0 -- each content can only physically exist in one parent
);

create index if not exists idx_content_contentType on content(contentType, deleted);
create index if not exists idx_content_literalType on content(literalType, deleted);
create index if not exists idx_content_parentId on content(parentId, deleted);

-- Later, if you need stuff to have multiple parents, they can be SOFT LINKS

-- To simplify, we're not letting content values be edited. as such, the
-- content's modify date will become the value date
create table if not exists content_values (
    id integer primary key,
    contentId int not null,
    `key` text not null,
    `value` text not null
);

create index if not exists idx_content_values_contentId on content_values(contentId);
create index if not exists idx_content_values_key on content_values(`key`);

-- Even though keywords could technically be stored in the values table, I
-- DON'T want any more performance issues. We can index this table BY the keyword
create table if not exists content_keywords (
    id integer primary key,
    contentId int not null,
    `value` text not null
);

create index if not exists idx_content_keywords_contentId on content_keywords(contentId);
create index if not exists idx_content_keywords_value on content_keywords(`value`, contentId);

-- Again, the user permissions are tied directly to the content. Edits
-- and such completely remove all old values and insert the new ones, but
-- a snapshot is made before that happens
create table if not exists content_permissions (
    id integer primary key,
    contentId int not null,
    userId int not null,
    `create` int not null, -- split for optimized searching
    `read` int not null,
    `update` int not null,
    `delete` int not null
);

create index if not exists idx_content_permissions_contentId on content_permissions(contentId);
create index if not exists idx_content_permissions_userread on content_permissions(userId, read);

create table if not exists content_votes (
    id integer primary key,
    contentId int not null,
    userId int not null,
    vote int not null, -- again an enum
    createDate text not null
);

-- need to quickly look up votes by content usually
create index if not exists idx_content_votes_contentId on content_votes(contentId);

create table if not exists content_watches (
    id integer primary key,
    contentId int not null,
    userId int not null,
    lastCommentId int not null, -- The last seen comment id
    lastActivityId int not null, -- a different id space, last seen activity (history)
    createDate text not null,
    editDate text -- may not be displayed, but important to track
);

create index if not exists idx_content_watches_contentId on content_watches(contentId);
create index if not exists idx_content_watches_userId on content_watches(userId);

-- this becomes the action table! you don't need anything else, but getting
-- creates into here... well if you save EVERY bit of history as well as the
-- currently expanded... yes this could work. Just make it even
create table if not exists content_history(
    id integer primary key,
    contentId int not null,
    action int not null, -- an enum, create read update delete
    snapshotVersion int not null,
    message text default null, -- a user or system generated message that helps understand this history
    `snapshot` blob not null, -- some formatted thing which represents the entire page data (hopefully compressed)
    createUserId int not null, -- this tells us the edit user and date
    createDate text not null
);

-- There could be a LOT of history, this is VERY important!
create index if not exists idx_content_history_contentId on content_history(contentId);
create index if not exists idx_content_history_createDate on content_history(createDate);

-- To make message searches more simple and performant(?), put the edit
-- data right in the comment. no linking
create table if not exists messages(
    id integer primary key,
    contentId int not null,
    createUserId int not null,
    createDate text not null,
    `text` text not null,
    editUserId int default null,
    editDate text default null,
    history text default null, -- most comments will NOT be edited, so this should be fine
    module text default null,
    receiveUserId int not null default 0, -- sometimes comments can be directly linked to a recipient
    deleted int not null default 0
);

create table if not exists message_values (
    id integer primary key,
    messageId int not null,
    `key` text not null,
    `value` text not null
);

create index if not exists idx_message_values_commentId on message_values(messageId);
create index if not exists idx_message_values_key on message_values(`key`);

-- TRY TO LIMIT INDEXES ON COMMENTS! I'm worried about the insert speed.
-- This index is useful for massive comment searches within particularly rooms.
-- Don't need to search EVERY comment in existence for values...
create index if not exists idx_message_contentId on messages(contentId, deleted, module);

-- IF READ PERFORMANCE BECOMES AN ISSUE AGAIN: index on createDate and deleted

--ONLY add the module index if you think you need it! Querying for module messages
--will GENERALLY be limited by date or id or something, so the table will be linearly
--scanned anyway. 
--create index if not exists idx_comment_module on comments(module, deleted);
