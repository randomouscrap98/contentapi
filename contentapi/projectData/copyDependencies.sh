# This file copies static files like Languages, DbMigrations, etc.
# Anything that isn't produced or managed by VsCode/etc.

# "DEST" is the destination to put the dependencies into
DEST="$1"

if [ "$DEST" == "" ]
then
	echo "You must provide a DEST!"
	exit 1
fi

echo "Copying dependencies to $DEST"
MIGDIR="$DEST/dbmigrations/"

# Warn: notice only publish is copied. This "CopyDependencies" is only for
# publish I guess. Stuff like "debug" is (currently) meant to be run locally,
# so it uses these dependencies directly from the folder.
cp -r LanguageFiles "$DEST"
mkdir -p "$MIGDIR"
cp dbmigrations/*.sql "$MIGDIR"
cp dbmigrations/publish/*.sql "$MIGDIR"

