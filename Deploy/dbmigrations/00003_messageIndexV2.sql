drop index if exists idx_message_contentId;
drop index if exists idx_message_contentmodule;

CREATE INDEX if not exists idx_message_standard on messages(contentId, id, module, deleted);
CREATE INDEX if not exists idx_message_standard_date on messages(contentId, createDate, module, deleted);
