phost=random@oboy.smilebasicsource.com
port=240
mtype=linux-x64
pfolder=/storage/random/contentapi
rsync='rsync -zz -avh -e "ssh -p $port"'
db=content.db
dfolder=projectData
# cdate='2019-01-01 00:00:00.000000'
# cdateid="select id from entities where createDate='$cdate'"
# dbdo="sqlite3 \"$db\""
corev=3.0
cwd=`pwd`
# -p:PublishSingleFile=true

hostrsync()
{
   rsync -zz -avh -e "ssh -p $port" "$1" "$phost:$2"
}

# This part might not be necessary soon, idk. Once you stop always overwriting, 
# this should become ok.
cd "$dfolder"
./extractSchema.sh
cd "$cwd"

# The project itself. Note that building/etc. puts basically everything you need
# into the destination folder (such as language files, dbmigrations, etc). Hooraaayyy.
dotnet publish -r $mtype -c Release 
hostrsync "./bin/Release/netcoreapp$corev/$mtype/publish/" "$pfolder"

hostrsync "$dfolder/dbmigrations" "$pfolder"
hostrsyng "$dfolder/migrate.sh" "$
ssh $phost -p $port "cd $pfolder; chmod 700 contentapi; \
 $dbdo \"insert into entities(createDate, status, baseAllow) values ('$cdate', 0, 6)\";\
 $dbdo \"insert into categoryEntities(entityId, name) values (($cdateid), 'sbs-main')\""
