OUTPUT="$1"
ALREADY="$2"
MIGRATIONS="dbmigrations"
SQL3="./sqlite3.exe"

if [ "$OUTPUT" == "" ]
then
	echo "You must provide a destination!"
	exit 1
fi

# NOTE: the migrations path can't have spaces! how to do??!?!
for sql in $MIGRATIONS/*.sql
do
	name=`basename $sql`
	mg="$ALREADY/$name"
	if [ "$ALREADY" != "" ] && [ -f "$mg" ]
	then
		echo "Skipping $name, already migrated"
	else
		$SQL3 "$OUTPUT" ".read $sql"
		if [ "$ALREADY" != "" ]
		then
			mkdir -p "$ALREADY"
			touch "$mg"
		fi
	fi
done