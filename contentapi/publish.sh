dotnet publish --configuration Release
rsync -zz -avh -e "ssh -p 240" "./bin/Release/netcoreapp2.2/publish/" random@oboy.smilebasicsource.com:/storage/random/contentapi
# Remove this or make it optional soon
rsync -zz -avh -e "ssh -p 240" "./content.db" random@oboy.smilebasicsource.com:/storage/random/contentapi
