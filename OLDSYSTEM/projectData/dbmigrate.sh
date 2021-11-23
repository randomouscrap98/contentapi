#!/bin/bash

set -e

DB=${1:-newcontent.db}
DBMIGRATIONS=${2:-dbMigrations}

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
