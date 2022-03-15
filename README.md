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
3. Navigate to wherever it tries to tell you the endpoint is (probably localhost:someport) and visit /api/status. You should see some server information in json form.

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
7. Check if it's running by going to your proxied (or whatever) endpoint and going to /api/status. You should see json output with information
   on the server.

**To RE-publish:**
1. Run the `Deploy/publish.sh` script
2. `sudo systemctl restart contentapi`

## Using the API
Assuming you have it running, there's some neat stuff you can do:

### Local Frontend
Navigate to the root and go to `/api/run/index.html`. This is a rudimentary frontend that consumes the api. You can do almost anything from here, including registering an account. By default, running from vscode uses a "null" emailer, so any user accounts you create will need to use the "backdoor" functions to get the registration key. Backdoor functions are only enabled for the local service, and maybe not even that (let me know if you have trouble). You can find your registration key by searching through the emails at `/api/user/emaillog` (you can just visit it in your browser).

The local frontend has VERY LITTLE error handling, and that's on purpose. I probably won't be adding it, even if issues are opened, because the point is to show you how to consume the api. Error handling muddies up the code examples.

Feel free to sift through the frontend to get a feel for how the api is used. I think using the frontend is the best way to see what the api is meant for, and what it provides.

#### api.js
The local frontend ALSO has a highly useful api consumer called "api.js", which you can download and use in your own applications which consumes the api. This can be found at `/api/run/api.js`. I recommend using api.js as your means to communicate with the api, as it saves a lot of time and effort. 

### Swagger
The api also has a builtin api browser called "swagger" available at `/swagger/index.html`. It lists all available endpoints and even shows the required submission object formats and a bit of the probable output formats. If you want to authenticate as a particular user (required for many things), you'll need to get your token from the `/api/user/login` endpoint, then click the "Authorize" button at the top right of swagger and enter `Bearer your_key`. Make sure there's a space between Bearer and the key. Swagger will be authenticated until the page is reloaded.

One of the first endpoints I'd recommend visiting is `/api/request/about`, as the "request" endpoint is how you get most of the data from the api. It allows you to make large requests and "chain" requests together by using data from previous requests. It allows SQL-like queries against the data, along with sorting / limits / field selection / etc. The request system requires its own documentation, so that will most likely all go inside of the `/api/request/about` endpoint.

## Issues / bugs
If you have issues, please let me know. Open a github issue if you like, I want this to be easily runnable.
