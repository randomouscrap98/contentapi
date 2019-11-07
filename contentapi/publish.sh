phost=random@oboy.smilebasicsource.com
port=240
mtype=linux-x64
pfolder=/storage/random/contentapi
rsync='rsync -zz -avh -e "ssh -p $port"'
db=content.db
cdate='2019-01-01 00:00:00.000000'
cdateid="select id from entities where createDate='$cdate'"
dbdo="sqlite3 \"$db\""
corev=3.0
# -p:PublishSingleFile=true

hostrsync()
{
   rsync -zz -avh -e "ssh -p $port" "$1" "$phost:$2"
}

dotnet publish -r $mtype -c Release 
hostrsync "./bin/Release/netcoreapp$corev/$mtype/publish/" "$pfolder"
# Remove this or make it optional soon
hostrsync "./$db" "$pfolder"
ssh $phost -p $port "cd $pfolder; chmod 700 contentapi; \
 $dbdo \"insert into entities(createDate, status, baseAllow) values ('$cdate', 0, 6)\";\
 $dbdo \"insert into categoryEntities(entityId, name) values (($cdateid), 'sbs-main')\""
