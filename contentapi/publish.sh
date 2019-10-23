dotnet publish -r linux-x64 -c Release
rsync -zz -avh -e "ssh -p 240" "./bin/Release/netcoreapp3.0/linux-x64/publish/" random@oboy.smilebasicsource.com:/storage/random/contentapi
# Remove this or make it optional soon
rsync -zz -avh -e "ssh -p 240" "./content.db" random@oboy.smilebasicsource.com:/storage/random/contentapi
