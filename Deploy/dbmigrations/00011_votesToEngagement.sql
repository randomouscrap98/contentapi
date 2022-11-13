BEGIN TRANSACTION;

-- Votes are changing to engagement; only modern versions of sqlite3 have drop column
create table if not exists content_engagement (
    id integer primary key,
    contentId int not null,
    userId int not null,
    `type` text not null, 
    engagement text not null default "", 
    createDate text not null
);

insert into content_engagement(id, contentId, userId, `type`, engagement, createDate)
select id, contentId, userId, 'vote' as `type`, 
  case vote when 1 then 'bad' when 2 then 'ok' when 3 then 'good' else '' end, createDate from content_votes;

-- Add those all important indexes for the new table
create index if not exists idx_content_engagement_userIdtype on content_engagement(userId, type);
create index if not exists idx_content_engagement_contentIdtype on content_engagement(contentId, type);

-- Now that we're done, we can drop the old table
drop table if exists content_votes;

-- Ugh right and now we need the same table but for messages...
create table if not exists message_engagement (
    id integer primary key,
    messageId int not null,
    userId int not null,
    `type` text not null, 
    engagement text not null default "", 
    createDate text not null
);

create index if not exists idx_message_engagement_contentIdtype on message_engagement(messageId, type);
create index if not exists idx_message_engagement_userIdtype on message_engagement(userId, type);

COMMIT;