phost=random@oboy.smilebasicsource.com
port=240
mtype=linux-x64
pfolder=/storage/random/contentapi
rsync='rsync -zz -avh -e "ssh -p $port"'
db=content.db
dfolder=projectData
corev=3.0
cwd=`pwd`
# -p:PublishSingleFile=true

hostrsync()
{
   rsync -zz -avh -e "ssh -p $port" "$1" "$phost:$2"
}

# The project itself.
dotnet publish -r $mtype -c Release 

# Now copy all the dependencies before we rsync
cd "$dfolder"
  # This part might not be necessary soon, idk.
  ./extractSchema.sh
  ./copyDependencies.sh "$pfolder"
cd "$cwd"

# Now put the stuff on the server!
hostrsync "./bin/Release/netcoreapp$corev/$mtype/publish/" "$pfolder"

# And then chmod + migrate!
ssh $phost -p $port "cd $pfolder; chmod 700 contentapi;"
