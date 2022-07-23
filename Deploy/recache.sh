#!/bin/sh

udate=`date +%s`
wwwroot="../contentapi/wwwroot"
files="$wwwroot/index.html $wwwroot/chat.html"

for f in $files
do
   echo "Recaching links in $f"
   cp "${f}" "${f}.bak"
   sed -i "s/?v=[[:digit:]]\+/?v=${udate}/g" "${f}"
done
