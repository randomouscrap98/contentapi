
ALTER TABLE users ADD COLUMN lastPasswordDate TEXT;

-- These are useless, the columns included are essentially sort orders. Both id
-- and createDate are unique, so additional columns only increase the index size 
-- (and potentially slow it down)
DROP INDEX IF EXISTS idx_message_standard_date;
DROP INDEX IF EXISTS idx_message_standard;

-- These just have a silly secondary sort order, it's unnecessary for the 
-- size of the set. Plus, for the use case, some should be sorted differently
-- than they are (they're all secondary sorted by delete before this)
DROP INDEX IF EXISTS idx_content_contentType;
DROP INDEX IF EXISTS idx_content_literalType;
DROP INDEX IF EXISTS idx_content_parentId;

-- Use desc because MOST searches will be for the latest items. It doesn't add much but...
CREATE INDEX if not exists idx_message_contentId_id on messages(contentId, id desc);
CREATE INDEX if not exists idx_message_contentId_createDate on messages(contentId, createDate desc);

-- Let secondary sort be ID now, checking for deleted is not necessary.
-- Content is not the thing that will require intensive indexing, not enough data.
-- Plus, this way, it's more expected (sorting by ID)

-- This is useful for filtering out the massive amount of files we have. We have
-- tens of thousands of files, perhaps will become even more, hundreds of thousands,
-- in the future, so finding standard content is important
CREATE INDEX if not exists idx_content_contentType on content(contentType);

-- NOTE: you don't need to initially sort by contentType, because internalType
-- will almost certainly not have overlaps across content types. For instance,
-- file literalTypes will be mimetypes, but content literalTypes will be chat,
-- program, etc. 
CREATE INDEX if not exists idx_content_literalType on content(literalType);
CREATE INDEX if not exists idx_content_parentId on content(parentId);

-- This makes sure everyone's password doesn't immediately expire
UPDATE users SET lastPasswordDate = '2022-04-31T00:00:00.000Z';
