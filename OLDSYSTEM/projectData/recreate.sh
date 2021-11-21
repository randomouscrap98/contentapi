rm -f dbMigrations/*.done
rm -f newcontent.db
./dbmigrate.sh
cp newcontent.db ../contentapi
