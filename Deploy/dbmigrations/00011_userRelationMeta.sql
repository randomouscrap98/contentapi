-- ALTER TABLE user_relations ADD COLUMN `meta` TEXT DEFAULT null;

create table if not exists user_content_relations (
    id integer primary key,
    type text not null,
    createDate text not null,
    userId integer not null,
    contentId integer not null,
    `meta` text default null
);

-- For finding all relations for a user of type (or just all for a user)
create index if not exists idx_user_content_relations_typeuserid on user_content_relations(userId, type); 
-- For finding all relatoins for a content of type (or just all for a content)
create index if not exists idx_user_content_relations_typeuserid on user_content_relations(contentId, type);

-- Will we ever be looking for user content relations JUST based on type alone? Probably not; always going to 
-- be limited by some content, like finding flags on posts