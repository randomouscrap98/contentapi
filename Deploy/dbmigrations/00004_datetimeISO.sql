BEGIN TRANSACTION;

update admin_log set createDate = REPLACE(createDate, ' ', 'T') || 'Z';
update bans set createDate = REPLACE(createDate, ' ', 'T') || 'Z';
update bans set `expireDate` = REPLACE(`expireDate`, ' ', 'T') || 'Z';
update content set createDate = REPLACE(createDate, ' ', 'T') || 'Z';
update content_history set createDate = REPLACE(createDate, ' ', 'T') || 'Z';
update content_votes set createDate = REPLACE(createDate, ' ', 'T') || 'Z';
update content_watches set createDate = REPLACE(createDate, ' ', 'T') || 'Z';
update content_watches set editDate = REPLACE(editDate, ' ', 'T') || 'Z';

DROP INDEX IF EXISTS idx_message_standard_date;
update messages set createDate = REPLACE(createDate, ' ', 'T') || 'Z';
update messages set editDate = REPLACE(editDate, ' ', 'T') || 'Z';
CREATE INDEX if not exists idx_message_standard_date on messages(contentId, createDate, module, deleted);

update user_relations set createDate = REPLACE(createDate, ' ', 'T') || 'Z';
update user_variables set createDate = REPLACE(createDate, ' ', 'T') || 'Z';
update user_variables set editDate = REPLACE(editDate, ' ', 'T') || 'Z';
update users set createDate = REPLACE(createDate, ' ', 'T') || 'Z';
update users set editDate = REPLACE(editDate, ' ', 'T') || 'Z';

COMMIT;