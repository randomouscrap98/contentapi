ALTER TABLE users ADD COLUMN createUserId INTEGER NOT NULL DEFAULT 0;

CREATE INDEX IF NOT EXISTS idx_users_type ON users(type, createUserId);
