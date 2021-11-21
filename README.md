# Contentapi
A simple API for servicing simple websites (in this case, sbs)

- This app uses dotnet core 6, you will need to get the sdk: https://dotnet.microsoft.com/download/dotnet/6.0
- You will also need sqlite3 on your system, unless you modify the deploy and product to use another database

## How to set up
There are a couple things to note:
- Everything you need to deploy this product is in the `Deploy` folder
- There are DB migrations, they create and update the structure of the database as this app expects it
- There are service files included in the `Deploy` folder, they are designed for our environments but you can modify them for your own use
- There is a `publish.sh` script which publishes to our environments. Again, you can modify these to suit your needs
- This app _assumes_ it is running behind a reverse proxy. As such, it does not handle https.

### To run locally
1. Run the `Deploy/recreate.sh` script IN the `Deploy` directory. This will create the database and copy it into the appropriate location
   for running in VS Code (`contentapi/content.db`)
2. Start debugging the app. Use defaults for launch settings in VS Code. 
   - Note that the included `appsettings.json` should be enough to get you going... most likely
3. Navigate to wherever it tries to tell you the endpoint is and visit /status. You should see some server information in json form.

If you have issues, please let me know. Open a github issue if you like, I want this to be easily runnable.

### Publish to a server
The most basic thing you can do is run `Deploy/publish.sh`. This will attempt to publish to OUR servers if you don't modify it.
There are no password logins for those accounts, you MUST have the private key. You can probably just modify the script to 
include your own server information. All of the important configuration is at the top, you almost certainly don't need to modify
anything past the first section of variables.

If you're setting up a systemd service on your server, you'll want to copy the `Deploy/contentapi.service` file onto your server.
Again, modify to your desired use case. Don't forget to run `sudo systemctl enable contentapi` the first time you're finished 
with it, so the service is active. You run `sudo systemctl daemon-reload` to refresh the system after editing the file AFTER
enabling it.

**Full steps:**
1. Set up the directory on your server/wherever which will house the binaries, settings, and sqlite database. You probably don't want global read on this folder...
2. Set up a `postinstall.sh` script in that same folder. The post install script will be called after the app is installed. 
   - I use it to set permissions and groups on all the files, and run the dbmigrations. 
   - To run the dbmigrations, just call `./dbmigrate.sh`. It can be run as many times as you want, it keeps track of which sql has been run already.
     It also makes a backup, but only once per call, each call overwrites the last backup. Be careful!
3. Run the `Deploy/publish.sh` script to get the files onto the server. This also copies resource files and the dbmigrations scripts (sql and sh)
4. After modifying to suit your needs, copy the service file to `/etc/systemd/system`, then as a first time install, run
   `sudo systemctl enable contentapi`
5. (Optional) Set up nginx or some other proxy to proxy connections to http://localhost:5000. Or whatever url you set in your service files
6. Check to make sure you have a `content.db` file in your server publish folder. Make sure the large amount of binaries are there. Make
   sure your appsettings.json and appsettings.Production.json (or whatever you're using) look fine. Then you can start your service
   with `sudo systemctl start contentapi`
7. Check if it's running by going to your proxied (or whatever) endpoint and going to /status. You should see json output with information
   on the server.

**To RE-publish:**
1. Run the `Deploy/publish.sh` script
2. `sudo systemctl restart contentapi`
