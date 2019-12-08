# Everything is relative to the folder this script is in
SQL3="./sqlite3.exe"
DBM="dbmigrations"

$SQL3 "content_base.db" ".schema" > "$DBM/aaa_create.sql"
