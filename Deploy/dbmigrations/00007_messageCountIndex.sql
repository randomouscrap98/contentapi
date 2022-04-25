-- So, we need a covering index for natural comment counts, otherwise this just doesn't work
CREATE INDEX IF NOT EXISTS idx_messages_forcounting ON messages(contentId, module, deleted);
