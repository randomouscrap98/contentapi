CREATE TABLE IF NOT EXISTS "userEntities" (
	"entityId"	INTEGER NOT NULL,
	"username"	TEXT NOT NULL UNIQUE,
	"passwordHash"	BLOB,
	"passwordSalt"	BLOB,
	"email"	TEXT NOT NULL UNIQUE,
	"registerCode"	TEXT,
	"role"	INTEGER NOT NULL DEFAULT 0,
	PRIMARY KEY("entityId"),
	FOREIGN KEY("entityId") REFERENCES "entities"("id") ON UPDATE CASCADE ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS "entityLog" (
	"id"	INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
	"userId"	INTEGER NOT NULL,
	"createDate"	TEXT NOT NULL,
	"action"	INTEGER NOT NULL,
	"entityId"	INTEGER NOT NULL,
	FOREIGN KEY("userId") REFERENCES "userEntities"("entityId") ON UPDATE CASCADE ON DELETE CASCADE,
	FOREIGN KEY("entityId") REFERENCES "entities"("id") ON UPDATE CASCADE ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS "entities" (
	"id"	INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
	"createDate"	TEXT NOT NULL,
	"status"	INTEGER NOT NULL DEFAULT 0,
	"userId"	INTEGER,
	"baseAllow"	INTEGER NOT NULL,
	FOREIGN KEY("userId") REFERENCES "userEntities"("entityId") ON UPDATE CASCADE ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS "entityAccess" (
	"id"	INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
	"entityId"	INTEGER NOT NULL,
	"userId"	INTEGER NOT NULL,
	"allow"	INTEGER NOT NULL,
	"createDate"	TEXT NOT NULL,
	FOREIGN KEY("userId") REFERENCES "userEntities"("entityId") ON UPDATE CASCADE ON DELETE CASCADE,
	FOREIGN KEY("entityId") REFERENCES "entities"("id") ON UPDATE CASCADE ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS "categoryEntities" (
	"entityId"	INTEGER NOT NULL,
	"name"	TEXT NOT NULL,
	"description"	TEXT,
	"type"	TEXT,
	"parentId"	INTEGER,
	FOREIGN KEY("entityId") REFERENCES "entities"("id") ON DELETE CASCADE ON UPDATE CASCADE,
	PRIMARY KEY("entityId"),
	FOREIGN KEY("parentId") REFERENCES "categoryEntities"("entityId") ON UPDATE CASCADE ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS "contentEntities" (
	"entityId"	INTEGER NOT NULL,
	"title"	TEXT,
	"content"	TEXT NOT NULL,
	"format"	TEXT,
	"type"	TEXT,
	"categoryId"	INTEGER NOT NULL,
	PRIMARY KEY("entityId"),
	FOREIGN KEY("entityId") REFERENCES "entities"("id") ON UPDATE CASCADE ON DELETE CASCADE,
	FOREIGN KEY("categoryId") REFERENCES "categoryEntities"("entityId") ON DELETE CASCADE ON UPDATE CASCADE
);
CREATE TABLE IF NOT EXISTS "commentEntities" (
	"entityId"	INTEGER NOT NULL,
	"content"	TEXT NOT NULL,
	"format"	TEXT,
	"parentId"	INTEGER NOT NULL,
	PRIMARY KEY("entityId"),
	FOREIGN KEY("entityId") REFERENCES "entities"("id") ON DELETE CASCADE ON UPDATE CASCADE,
	FOREIGN KEY("parentId") REFERENCES "contentEntities"("entityId") ON UPDATE CASCADE ON DELETE CASCADE
);
CREATE INDEX "commentParentIndex" ON "commentEntities" (
	"parentId"
);
CREATE INDEX "accessEntityIndex" ON "entityAccess" (
	"entityId"
);
