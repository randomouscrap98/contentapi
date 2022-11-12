-- Votes are changing to engagement
ALTER TABLE content_votes RENAME TO content_engagement;

-- Need to rename a column
ALTER TABLE content_engagement ADD COLUMN `type` TEXT NOT NULL DEFAULT "";

update content_engagement set type = "vote";

-- Here, 'engagement' is replacing 'vote'. We add the new column for the engagement, 
-- which the votes will be translated into. Afterwards, we remove the old column 'vote'
ALTER TABLE content_engagement ADD COLUMN engagement TEXT NOT NULL DEFAULT "";
update content_engagement set engagement = "bad" where vote = 1;
update content_engagement set engagement = "ok" where vote = 2;
update content_engagement set engagement = "good" where vote = 3;
ALTER TABLE content_engagement DROP COLUMN vote;

-- With new "type" field, upgrade the existing index
drop index if exists idx_content_votes_contentId;
create index if not exists idx_content_engagement_contentIdtype on content_engagement(contentId, type);

-- And add one for users too
create index if not exists idx_content_engagement_userIdtype on content_engagement(userId, type);

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
