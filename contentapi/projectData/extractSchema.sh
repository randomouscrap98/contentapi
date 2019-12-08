# Everything is relative to the folder this script is in
SQL3="sqlite3"
DBM="dbmigrations"
SCHPRE="aaa_create"
# Can't put date in filename due to dbmigrations (even though they're separate files). 
# Otherwise, some systems will try to run alllll the dbmigrations
# DTNAME=`date +"%Y_%m_%d_%I_%M_%p"`

# rm -rf "$DBM/${SCHPRE}*"
# $SQL3 "content_base.db" ".schema" > "$DBM/${SCHPRE}_$DTNAME.sql"
$SQL3 "content_base.db" ".schema" > "$DBM/${SCHPRE}.sql"