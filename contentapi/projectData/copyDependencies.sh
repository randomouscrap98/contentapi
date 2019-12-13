DEST="$1"

if [ "$DEST" == "" ]
then
	echo "You must provide a DEST!"
	exit 1
fi

echo "Copying dependencies to $DEST"
MIGDIR="$DEST/dbmigrations/"

cp -r LanguageFiles "$DEST"
mkdir -p "$MIGDIR"
cp dbmigrations/*.sql "$MIGDIR"
cp dbmigrations/publish/*.sql "$MIGDIR"