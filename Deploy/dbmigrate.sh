#!/bin/bash

set -e

DB=${1:-content.db}
DBMIGRATIONS=${2:-dbmigrations}

# Make a backup of the current db, if it exists
if [ -e $DB ]
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
