#!/bin/sh

udate=`date +%s`
files=contentapi/wwwroot/index.html
cp "${files}" "${files}.bak"
sed -i "s/?v=[[:digit:]]\+/?v=${udate}/g" "${files}"
