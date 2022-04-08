ALTER TABLE users ADD COLUMN createUserId INTEGER;

CREATE INDEX IF NOT EXISTS idx_users_type ON users(type, createUserId);