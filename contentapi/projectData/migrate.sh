OUTPUT="$1"
ALREADY="$2"
MIGRATIONS="dbmigrations"
SQL3="sqlite3"

if [ "$OUTPUT" == "" ]
then
	echo "You must provide a destination!"
	exit 1
fi

if [ "$ALREADY" == "" ]
then
	echo "WARNING: Completely recreating $OUTPUT"
	rm -f "$OUTPUT"
fi

# NOTE: the migrations path can't have spaces! how to do??!?!
for sql in $MIGRATIONS/*.sql
do
	name=`basename $sql`
	if [ "$ALREADY" != "" ] && [ -f "$ALREADY/$name" ]
	then
		echo "Skipping $name, already migrated"
	else
		$SQL3 "$OUTPUT" ".read $sql"
		if [ "$ALREADY" != "" ]
		then
			mkdir -p "$ALREADY"
			cp "$sql" "$ALREADY"
		fi
	fi
done