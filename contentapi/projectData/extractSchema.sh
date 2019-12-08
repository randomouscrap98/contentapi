# Everything is relative to the folder this script is in
SQL3="sqlite3"
DBM="dbmigrations"
DTNAME=`date +"%Y_%m_%d_%I_%M_%p"`

$SQL3 "content_base.db" ".schema" > "$DBM/aaa_create_$DTNAME.sql"
