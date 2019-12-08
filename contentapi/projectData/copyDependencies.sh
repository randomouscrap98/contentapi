DEST="$1"

if [ "$DEST" == "" ]
then
	echo "You must provide a DEST!"
	exit 1
fi

cp -r LanguageFiles "$DEST"
cp -r dbmigrations "$DEST"