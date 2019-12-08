# Everything is relative to the folder this script is in
SQL3="sqlite3"
DBM="dbmigrations"
SCHPRE="aaa_create"
DTNAME=`date +"%Y_%m_%d_%I_%M_%p"`

rm -rf "$DBM/$SCHPRE*.sql"
$SQL3 "content_base.db" ".schema" > "$DBM/$SCHPRE_$DTNAME.sql"
