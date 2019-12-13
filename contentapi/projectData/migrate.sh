OUTPUT="$1"
ALREADY="$2"
INCLUDE="$3"
MIGRATIONS="dbmigrations"
SQL3="sqlite3"

if [ "$OUTPUT" == "" ]
then
	echo "You must provide a destination!"
	exit 1
fi

if [ "$ALREADY" == "" ]
then
	echo " * Completely recreating $OUTPUT"
	rm -f "$OUTPUT"
fi

echo "Migrating from $MIGRATIONS into $OUTPUT"

migrate()
{
	# NOTE: the migrations path can't have spaces! how to do??!?!
	for sql in $1/*.sql
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
}

migrate "$MIGRATIONS"

if [ "$INCLUDE" != "" ]
then
	for extra in "$INCLUDE"
	do
		echo "Migrating extra: $extra"
		migrate "$MIGRATIONS/$extra"
	done
fi