# General publish script for dotnet core projects

name="contentapi"
pfolder="/var/www/${name}"                  # The REMOTE location to PLACE all files
phost=publisher@oboy.smilebasicsource.com   # The default server (development) (and user to connect)
pfolder="/storage/random/${name}"           # The REMOTE location to PLACE all files
rdf="rl"
port=240

if [ "$1" = "production" ]
then
    echo "WARN: PUBLISHING PRODUCTION"
    phost=publisher@smilebasicsource.com   # The default server (development) (and user to connect)
    pfolder="/var/www/${name}"
    port=22
fi

if [ "$1" = "sbsdev" ]
then
    echo "Publishing SBS development"
    pfolder="/storage/random/sbs_${name}"
fi

if [ "$1" = "sbsproduction" ]
then
    echo "Publishing SBS PRODUCTION!!"
    phost=publisher@smilebasicsource.com   # The default server (development) (and user to connect)
    pfolder="/var/www/sbs/${name}"
    port=22
fi

# space separated
deploy="Deploy"
copyfolders="$deploy/dbmigrate.sh $deploy/dbmigrations"
removefiles="ignore uploads content.db dbmigrations/*.done"

# Eventually, may include some kind of language system
# copyfolders="$deploy/LanguageFiles $deploy/dbmigrate.sh $projectdata/dbMigrations"

# Stuff for dotnet
projectfolder=${name}
mtype=linux-x64         # The architecture of the target machine
corev=6.0               # The version of dotnet core we're using
projfile=${projectfolder}/${name}.csproj # The project file for version updating / etc
bconfig=Release         # Build configuration (if you have one)
lpfolder="${name}/bin/${bconfig}/net$corev/$mtype/publish/"   # The LOCAL location to retrieve binaries

postinstallscript="postinstall.sh"
postinstallargs=""

# Function for syncing to the globally configured location
hostrsync()
{
   rsync -zz -${rdf}vh -e "ssh -p $port" "$1" "$phost:$2"
}

echo "Publishing to $phost:$pfolder"

# Recache works from in THIS folder
sh recache.sh

# Work from the root folder!
cd ..

# Increment the last digit of the "version" xml (yeah yeah regex whatever, I know what's in csproj)
sed -i "s/<Version>[0-9]*[0-9]\.[0-9]*[0-9]\.[0-9]*[0-9]\.[0-9]*[0-9]/&@/g;:a {s/0@/1/g;s/1@/2/g;s/2@/3/g;s/3@/4/g;s/4@/5/g;s/5@/6/g;s/6@/7/g;s/7@/8/g;s/8@/9/g;s/9@/@0/g;t a};s/@/1/g" ${projfile}


# We REMOVE the local publish folder to get rid of old, no longer needed files
# (don't want to sync unnecessary or even harmful things).
# A dotnet publish SHOULD do everything required to make the product. It just
# doesn't include our personal extras (it probably could though)
rm -rf "$lpfolder"

# This next stuff needs to operate in the actual app folder
# WARN: DON'T PUBLISH SINGLE! IT FILLS THE TEMP DIRECTORY!!
(cd ${name} && dotnet publish -r $mtype --self-contained -c ${bconfig})

# Copy desired files into publish folder
for cpyfl in $copyfolders
do
    cp -r $cpyfl "$lpfolder"
done

# Remove undesired files from publish folder
for rmfl in $removefiles
do
    rm -f "$lpfolder"/${rmfl}
done

# Now put the stuff on the server! A simple direct copy
hostrsync "$lpfolder" "$pfolder"

# And then run the post install! This is a script already on the server that knows more what to do per environment.
ssh $phost -p $port "cd $pfolder; test -f $postinstallscript && ./$postinstallscript $postinstallargs"
