phost=random@oboy.smilebasicsource.com
port=240
mtype=linux-x64
pfolder=/storage/random/contentapi
rsync='rsync -zz -avh -e "ssh -p $port"'
# -p:PublishSingleFile=true

hostrsync()
{
   rsync -zz -avh -e "ssh -p $port" "$1" "$phost:$2"
}

dotnet publish -r $mtype -c Release 
hostrsync "./bin/Release/netcoreapp3.0/$mtype/publish/" "$pfolder"
# Remove this or make it optional soon
hostrsync "./content.db" "$pfolder"
ssh $phost -p $port "cd $pfolder; chmod 700 contentapi"
