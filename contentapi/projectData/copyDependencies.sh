# This file copies static files like Languages, DbMigrations, etc.
# Anything that isn't produced or managed by VsCode/etc.

# "DEST" is the destination to put the dependencies into
DEST="$1"
DBMNAME="dbmigrations"

if [ "$DEST" == "" ]
then
	echo "You must provide a DEST!"
	exit 1
fi

echo "Copying dependencies to $DEST"
# MIGDIR="$DEST/"

# Warn: ALL dbmigrations are copied (including ones for various systems). This
# shouldn't be a problem in practice, just keep it in mind.
cp -r LanguageFiles "$DEST"
cp -r "$DBMNAME" "$DEST"
# mkdir -p "$MIGDIR"
# cp dbmigrations/*.sql "$MIGDIR"
# cp dbmigrations/publish/*.sql "$MIGDIR"

