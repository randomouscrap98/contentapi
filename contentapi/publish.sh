# This "Publish" script SHOULD perform ALL steps necessary to make the defined
# server be "production ready". This means ONLY production is supported by 
# this script. If another environment should be setup for "testing", this 
# script (and others) must be upgraded. Also note: this is not set up to be a
# service, it is instead run as myself.
# TODO: THIS IS NOT PRODUCTION READY! Make the product a SERVICE run and owned
# by a user other than yourself, and restart the service upon installation.

# Publish steps: 
# - Completely rebuild the binaries to local publish
# - Extract schema from master database
# - Copy extra dependencies (langauge, dbmigrations) to local publish
# - Copy publish to remote at install location
# - Set up remote system (permissions, services(?), dbmigrations, etc)

# Stuff for connecting
phost=random@oboy.smilebasicsource.com # The production server (and user to connect)
port=240
rsync='rsync -zz -avh -e "ssh -p $port"' 

# Stuff for dotnet
mtype=linux-x64      # The architecture of the target machine
corev=3.1            # The version of dotnet core we're using

# My places!
lpfolder="./bin/Release/netcoreapp$corev/$mtype/publish/"   # The LOCAL location to retrieve binaries
pfolder="/storage/random/contentapi"                        # The REMOTE location to PLACE all files
cwd="`pwd`"

echo "Publishing to $pfolder"

hostrsync()
{
   rsync -zz -avh -e "ssh -p $port" "$1" "$phost:$2"
}

# The project itself. Delete the old folder (probably).
# We REMOVE the local publish folder to get rid of old, no longer needed files
# (don't want to sync unnecessary or even harmful things).
# A dotnet publish SHOULD do everything required to make the product. It just
# doesn't include our personal extras (it probably could though)
rm -rf "$lpfolder"
dotnet publish -r $mtype -c Release -p:PublishSingleFile=true

# You need these, and they're likely to be the same all the time on every system
cp -r LanguageFiles "$lpfolder"

# Now put the stuff on the server! A simple direct copy
hostrsync "$lpfolder" "$pfolder"

# And then chmod! The main running file should be executable
ssh $phost -p $port "cd $pfolder; chmod 700 contentapi;"
