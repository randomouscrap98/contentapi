DEST="$1"

if [ "$DEST" == "" ]
then
	echo "You must provide a DEST!"
	exit 1
fi

echo "Copying dependencies to $DEST"

cp -r LanguageFiles "$DEST"
cp -r dbmigrations "$DEST"