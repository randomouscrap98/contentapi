# This performs a list of dbmigrations. It can either run them all or use an
# "Already" directory to figure out which dbmigrations have "already" been run.
# This is useful for a production environment.

OUTPUT="$1"    # The destination database file to apply migrations to
ALREADY="$2"   # The folder describing sql already run
INCLUDE="$3"   # The "extra" migrations to run (for individual environments)
MIGRATIONS="dbmigrations"  # The location of migration sql
SQL3="sqlite3"             # The command to run sql

# User MUST provide which database to apply to
if [ "$OUTPUT" == "" ]
then
	echo "You must provide a destination!"
	exit 1
fi

if [ "$ALREADY" == "NUL" ]
then
   ALREADY=""
fi

# If user does not specify "already" folder, assume the entire database is
# getting recreated (delete existing database)
if [ "$ALREADY" == "" ]
then
	echo " * Completely recreating $OUTPUT"
	rm -f "$OUTPUT"
fi

echo "Migrating from $MIGRATIONS into $OUTPUT"

# Migration is simply running all SQL files DIRECTLY inside the folder we want
# (passed as first parameter). Skip sql already run
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

# ALWAYS migrate the base folder
migrate "$MIGRATIONS"

# IF PROVIDED, migrate the extra folders
if [ "$INCLUDE" != "" ]
then
	for extra in "$INCLUDE"
	do
		echo "Migrating extra: $extra"
		migrate "$MIGRATIONS/$extra"
	done
fi
