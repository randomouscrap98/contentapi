#!/bin/bash

set -e

DB=${1:-content.db}
BACKUP=${2:-content.db.bak}
DBMIGRATIONS=${3:-dbmigrations}

# Make a backup of the current db, if it exists
if [ -e "$DB" ]
then 
   rm -f "$BACKUP"
   echo "Backing up db to $BACKUP"
   sqlite3 "$DB" ".backup $BACKUP"
   # if [ -n "$BACKUP" ]
   # then
   #    BACKUP=${BACKUP}/${DB}_`date +"%Y%m%d_%H%M%S"`.tar.gz
   #    echo "Backing up db as tar archive: $BACKUP"
   #    tar -czf "$BACKUP" "$DB"
   # else
   #    cp "$DB" "$DB.bak"
   # fi
fi

for f in $DBMIGRATIONS/*.sql
do
   df=$f.done
   if [ -r $df ]
   then
      continue
   fi
   echo "Processing $f..."
   cat $f | sqlite3 "$DB"
   touch $df
done
