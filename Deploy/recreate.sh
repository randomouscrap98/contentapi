#!/bin/bash

set -e

rm -f dbMigrations/*.done
rm -f content.db
./dbmigrate.sh
cp content.db ../contentapi
