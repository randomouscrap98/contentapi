#!/bin/bash

set -e

DB=${1:-content.db}
DBMIGRATIONS=${2:-dbMigrations}

# Make a backup of the current db, if it exists
if [ -e $db ]
then 
   cp $DB $DB.bak
fi

for f in $DBMIGRATIONS/*.sql
do
   df=$f.done
   if [ -r $df ]
   then
      continue
   fi
   echo "Processing $f..."
   cat $f | sqlite3 $DB
   touch $df
done

