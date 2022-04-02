drop index if exists idx_content_history_createDate;
drop index if exists idx_message_createDate;

drop index if exists idx_message_contentId;
drop index if exists idx_content_history_contentId;

-- Unfortunately, need BOTH types of indexes because of something I don't understand. Having the singular
-- index with contentId, module, createDate seemed to not work, probably because "module" has very little 
-- in the way of unique entries (most are null). And whether through some glitch or otherwise, having
-- just contentId and module seems to solve that problem... weird. Maybe createDate makes the index
-- too large and that's why it's hard to search through (this seems likely)
create index if not exists idx_message_contentid on messages(contentId, createDate, module);
create index if not exists idx_message_contentmodule on messages(contentId, module);
create index if not exists idx_content_history_contentId on content_history(contentId, createDate);