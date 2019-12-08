# Stuff for connecting
phost=random@oboy.smilebasicsource.com
port=240
rsync='rsync -zz -avh -e "ssh -p $port"'

# Stuff for dotnet
mtype=linux-x64
corev=3.0

# My places!
lpfolder="./bin/Release/netcoreapp$corev/$mtype/publish/"
pfolder=/storage/random/contentapi
db=content.db
dfolder=projectData

cwd=`pwd`
# -p:PublishSingleFile=true

hostrsync()
{
   rsync -zz -avh -e "ssh -p $port" "$1" "$phost:$2"
}

# The project itself. Delete the old folder (probably).
rm -rf "$lpfolder="
dotnet publish -r $mtype -c Release 

# Now copy all the dependencies before we rsync
cd "$dfolder"
  # This part might not be necessary soon, idk.
  ./extractSchema.sh
  ./copyDependencies.sh "$cwd/$lpfolder"
cd "$cwd"

# Now put the stuff on the server!
hostrsync "$lpfolder" "$pfolder"

# And then chmod + migrate!
ssh $phost -p $port "cd $pfolder; chmod 700 contentapi;"
