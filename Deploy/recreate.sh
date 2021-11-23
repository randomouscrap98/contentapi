#!/bin/bash

set -e

DB=content.db

# WARN: THIS RESETS YOUR LOCAL DATABASE! NOT MEANT FOR PRODUCTION SERVERS!!!
rm -f dbmigrations/*.done
rm -f $DB
./dbmigrate.sh $DB
cp $DB ../contentapi
