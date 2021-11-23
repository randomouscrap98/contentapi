create table if not exists users (
    id int primary key,
    username text not null,
    avatar int not null default 0,
    createDate text not null,
    special text,
    -- need bans, there's a bool banned in users
    -- also need to mark supers, they're pulled from a file for some reason.
    -- maybe change that?
    super int not null default 0,
    editDate text,
    -- user view also has ban object, represents current ban
    email text not null,
    hidelist text,
    password text not null,
    salt text not null,
    registrationKey text
);

create table if not exists bans (
    id int primary key,
    createDate text not null,
    expireDate text not null,
    createUserId int not null,
    bannedUserId int not null,
    message text,
    type int not null  -- all enums should be stored as integers for performance
);

-- Note: Only create indexes if you NEED them!

create table if not exists content (
    id int primary key,
    deleted int not null default 0, -- deleting removes the id, don't want that.
    createDate text not null,
    createUserId int not null,
    internalType int not null, --page, file, module, category etc (use an enum)
    publicType int not null,
    `name` text not null,
    content text not null,
    parentId int -- each content can only physically exist in one parent
);

create index if not exists idx_content_internalType on content(internalType, deleted);
create index if not exists idx_content_publicType on content(publicType, deleted);
create index if not exists idx_content_parentId on content(parentId, deleted);

-- Later, if you need stuff to have multiple parents, they can be SOFT LINKS

-- To simplify, we're not letting content values be edited. as such, the
-- content's modify date will become the value date
create table if not exists content_values (
    id int primary key,
    contentId int not null,
    `key` text not null,
    `value` text not null
);

create index if not exists idx_content_values_contentId on content_values(contentId);
create index if not exists idx_content_values_key on content_values(`key`);

-- Again, the user permissions are tied directly to the content. Edits
-- and such completely remove all old values and insert the new ones, but
-- a snapshot is made before that happens
create table if not exists content_permissions (
    id int primary key,
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
    id int primary key,
    contentId int not null,
    userId int not null,
    vote int not null, -- again an enum
    createDate text not null
);

-- need to quickly look up votes by content usually
create index if not exists idx_content_votes_contentId on content_votes(contentId);

create table if not exists content_watches (
    id int primary key,
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
    id int primary key,
    contentId int not null,
    action int not null, -- an enum, create read update delete
    `snapshot` text not null, -- some formatted thing which represents the entire page data (hopefully compressed)
    createUserId int not null, -- this tells us the edit user and date
    createDate text not null
);

-- There could be a LOT of history, this is VERY important!
create index if not exists idx_content_history_contentId on content_history(contentId);
create index if not exists idx_content_history_createDate on content_history(createDate);

-- To make comment searches more simple and performant(?), put the edit
-- data right in the comment. no linking
create table if not exists comments(
    id int primary key,
    contentId int not null,
    createUserId int not null,
    createDate text not null,
    receiveUserId int default null, -- sometimes comments can be directly linked to a recipient
    `text` text not null,
    editUserId int default null,
    editDate text default null,
    history text default null, -- most comments will NOT be edited, so this should be fine
    deleted int not null default 0
);

create index if not exists idx_comment_contentId on comments(contentId, deleted);