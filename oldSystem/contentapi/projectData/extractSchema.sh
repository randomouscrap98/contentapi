# This extracts ONLY the schema from our "base" content database. We manage the
# structure of data/tables using content_base.db as a sort of "master". This
# allows the use of the SQLite Manager (UI) which has nice tools.

# Everything is relative to the folder this script is in
SQL3="sqlite3"
DBM="dbmigrations"
SCHPRE="aaa_create"              # We want the schema run first, thus aaa
OUTFILE="$DBM/${SCHPRE}.sql"
BASE="content_base.db"

echo "Extracting base schema from $BASE to $OUTFILE"
# Can't put date in filename due to dbmigrations (even though they're separate files). 
# Otherwise, some systems will try to run alllll the dbmigrations
# DTNAME=`date +"%Y_%m_%d_%I_%M_%p"`

# rm -rf "$DBM/${SCHPRE}*"
# $SQL3 "content_base.db" ".schema" > "$DBM/${SCHPRE}_$DTNAME.sql"

# Extract the schema, but remove the sqlite_sequence table. This causes
# problems (I don't know why it gets exported in the first place)
$SQL3 "$BASE" ".schema" | sed "/CREATE TABLE sqlite_sequence/d" > "$OUTFILE"
