//haloopdy - 2022/01
//A default implementation for connecting to our API. Feel free to use anywhere

//Some simple constants that should never change... hopefully
var APICONST = {
    STATUS : {
        BADREQUEST : 400,
        TOKENERROR : 401,
        NOTFOUND : 404,
        BANNED : 418,
        RATELIMIT : 429,
        NETWORKERROR : 9999
    },
    MARKUP : [ "12y", "bbcode", "plaintext" ],
    WRITETYPES : {
        MESSAGE : "message",
        CONTENT : "content",
        USER : "user"
    }
};

// -- API control / service objects --

//An object that represents an API success result. You got data back
function ApiResult(result, id, request)
{
    this.result = result;
    this.id = id;
    this.request = request; //This SHOULD have all the information you could possibly need...
}

//An object that represents an API error. There MAY be data, you can inspect the request
function ApiError(message, id, request)
{
    this.message = message || "";
    this.id = id;
    this.request = request; //This SHOULD have all the information you could possibly need...
    this.status_code = 0;   //This should be a duplicate of the code in request, but sometimes it isn't. Use this code for error handling...?
}

//An object which tells how to handle an API call, whether it succeeded or failed. Functions that accept
//these handlers aren't picky, and will work with simple js {} objects instead
function ApiHandler(success, error, always)
{
    this.always = always || false;      //What to "always" do, regardless of failure or not (EXCEPT network errors). "Always" is called at the start of any api response, if provided
    this.success = success || false;    //What to do when the call was successful. Depending on the call, you might get raw data or parsed
    this.error = error || { };          //This might seem strange, but the error handler is actually a dictionary. 0 is the default handler, any other code is the handler for that code
}


// The standard format of a request made over websocket, no matter what it is. The "type"
// determines what it does; some options are "ping" and "request". The "response" object
// you get back is nearly identical, just with data set to whatever you requested rather
// than what you sent. The ID is unnecessary, but I HIGHLY ENCOURAGE you to use it: your
// response will have the same ID you sent, so it helps you keep track of things
function WebsocketRequest(type, data, id, token)
{
    this.type = type;
    this.data = data;
    this.id = id;
    this.token = token;
}


// -- API reference objects --
// NOTE: These are here for reference. You CAN use them if you want, or you can simply ignore them
// and pass in your own objects with the same fields. The API will work either way.

// This is the data you'd need to submit a NEW comment, not necessarily the format of the comments that are 
// returned from the API. Many fields are computed automatically. Only "text" and "contentId" is truly 
// required of a comment though.
function NewCommentParameter(text, contentId, markup, avatar, nickname)
{
    this.id = 0; //Should be 0 for new comments; set to an id for an edit
    this.contentId = contentId;
    this.text = text; 
    this.markup = markup || null;
    this.avatar = avatar || null;
    this.nickname = nickname || null;
}

// The data you could provide when uploading a brand new file, NOT the format of file metadata
// as retrieved from the API however!
function UploadFileParameter(fileBlob, name, quantize, globalPerms, tryResize)
{
    this.fileBlob = fileBlob;   //Technically only this is required!
    this.name = name;           //All the rest default to whatever is passed, including undefined!
    this.quantize = quantize;
    this.globalPerms = globalPerms;
    this.tryResize = tryResize;
}

// You only need EITHER username OR email, not both
function LoginParameter(username, password, email, expireSeconds)
{
    this.username = username;
    this.password = password;
    this.email = email;
    this.expireSeconds = expireSeconds; //Without this, some default value is chosen
}

// This one, you need all parameters
function RegisterParameter(username, email, password)
{
    this.username = username;
    this.email = email;
    this.password = password;
}

function ConfirmRegistrationParameter(email, key)
{
    this.email = email;
    this.key = key;
}

function FileModifyParameter(size, crop, freeze)
{
    this.size = size;
    this.crop = crop;
    this.freeze = freeze;
}

// The main configuration object for ALL searches. Send this directly to the "Search" endpoint. 
// This should be a direct reflection of "SearchRequests" within the API C# code.
function RequestParameter(values, requests)
{
    //A dictionary of string to object relationships. Values can be integers, strings, lists, and maybe some other things?
    this.values = values || {}; 

    //A list of request types, which can reference each other. They are each in the format of RequestSearchParameter. 
    //They are run in the order they are provided, all within the same search request.
    this.requests = requests || [];
}

// A single search request for a single type. You can send multiple of these within a search
// request, all packaged above in "RequestParameter".
// This should be a direct reflection of "SearchRequest" within the API C# code.
function RequestSearchParameter(type, fields, query, order, limit, skip, name)
{
    this.type = type;               // Absolutely required; indicates which table you're searching in
    this.fields = fields || "*";    // Fields are essentially the "select *" in sql, you can provide which fields you want returned to optimize the query. Certain fields are FAR more expensive than others.
    this.query = query || "";       // Query is a subset of SQL, and allows you to do things like "username = @username", where @username is a value provided in RequestParameter above.
    this.order = order || "";       // Order should be the name of the field you want to order the results for this one search by. Some fields are NOT orderable. Add _desc to the name to order by descending
    this.limit = limit || 1000;     // NOTE: No matter what you put, the max limit will ALWAYS be 1000. However, you can certainly ask for fewer results
    this.skip = skip || 0;          // Skip is how many results to "skip" before beginning iteration. This allows for pagination, as you can skip 50, 100, 150, etc
    this.name = name;               // name isn't required, it will automatically be provided (except in special circumstances)
}

// NOTES ABOUT "query" in RequestSearchParameter:
// - query is a very powerful feature of the API. It allows you to do the traditional "chaining" from the previous
//   api (if you used it) while having even more features and being significantly easier to construct
// - It is almost exactly like writing sql. It supports arbitrary grouping operators (), AND/OR/LIKE/NOT,
//   and even some macros predefined within the API. 
// - For instance, if you want to lookup a particular page by ID, you might construct a query field like:
//   "id = @id", where "id" is the name of a value you providded in the "values" field for RequestParameter.
//   This means that all values are shared between all search requests in a single request. This is important,
//   because the RESULTS of each search ALSO become values!
// - So, let's say you now want to get the user for that page you just looked up. In another search (for users),
//   you can set query to:
//   "id in @page.createUserId". Notice that we're using "in" this time. This is because "page" is a result set,
//   so there could be multiple users. The name of previous result sets are simply the type, OR the "name"
//   field provided for that search, if you set one. You use the dot operator to select which field you want
//   to search against. You can also use "not in".
// - Thus, you provide a starter set of values for your searches, which are then added to with each successive
//   search. All values are treated equally, so you can provide your own objects, lists of objects, or whatever
//   as values, and construct queries like sql against anything. Just note that you CAN'T use literals within
//   the "query" field. This is for safety and speed reasons, I'm sorry. 

// For examples on how to use the request/search endpoint, see the preconstructed search examples
// at the bottom of the page.


// -- The API interface itself --

//The API object, which you instantiate and use as appropriate
function Api(url, tokenGet)
{
    this.url = url || "../";    //Access the current API by default. MUST END IN SLASH!
    this._next_request_id = 1;  //Internal: the ID to stamp the next request with. 

    //The user token to be used by any call in this API instance. It must be a function in order to be lenient about how 
    //the token is provided. If you don't need that leniency, make a function that always returns the same value
    this.get_token = tokenGet || (() => {
        console.warn("No token getter function set for api! Set 'get_token' on your api object");
        return false;
    });

    //Modify these default handlers if you don't need unique handlers per call
    this.default_handler = new ApiHandler(
        d => {
            alert("No default success handler set for api!");
            console.log("Data from API: ", d);
        },
        e => {
            alert("No default error handler set for api! See console for error");
            console.log("Error from API: ", e);
        },
        d => {
            console.debug(`api[${d.id}]: '${d.request.responseURL}' ${d.request.status} - ${d.request.responseText.length} bytes`);
        }
    );

    //The list of fields in ANY type of object that usually links to a user (not exhaustive, probably)
    this.userAutolinks = {
        createUserId: "createUser",
        editUserId: "editUser"
    };

    //The list of fields in ANY type of object that usually links to content (not exhaustive, probably)
    this.contentAutolinks = {
        contentId: "content",
        parendId: "parent"
    };
}

//Find the error handler function within the handler field, if it exists. This is because the error handler
//can either be a simple function (which handles all errors), or a dictionary of values where the key is the
//status code for that particular error. This usage isn't necessary to use at all, but is provided in case
//someone finds it useful.
Api.prototype.GetErrorHandler = function(handler, error)
{
    if(handler)
    {
        if(typeof(handler) === "function")
            return handler;
        else if(handler[error.code])
            return handler[error.code];
        else if(handler[0])
            return handler[0];
    }

    return null;
};

//Attempt to handle the given error with either the given handler (if possible) or our internal error handler.
Api.prototype.HandleError = function(handler, error)
{
    var realHandler = this.GetErrorHandler(handler, error) || this.GetErrorHandler(this.default_handler.error, error);

    if(!realHandler)
        throw "No API error handler set!";

    realHandler(error);
};


//Access any endpoint in the API. "path" is appended to whatever url we're using as the base for the API, and the call 
//is by default GET unless "postData" is set, in which case it is POST. The API uses no other verbs.
Api.prototype.Raw = function(path, postData, handler, modifyRequest, parseData) 
{
    var me = this;

    handler = handler || {};
    parseData = parseData || (x => JSON.parse(x)); //NOTE: EVERYTHING should be json, so default to this.
    var always = handler.always || me.default_handler.always;
    var success = handler.success || me.default_handler.success;

    var request = new XMLHttpRequest();
    var method = postData ? "POST" : "GET";
    var url = me.url + path; //Path can include query parameters if you want

    //This is a silly way of doing this, but to the end user, they won't really see it...
    var result = new ApiResult(false, me._next_request_id++, request);
    var error = new ApiError(false, result.id, request);

    request.addEventListener("error", function()
    {
        error.message = "Network error";
        error.status_code = APICONST.STATUS.NETWORKERROR;
        me.HandleError(handler, error);
        request.isNetworkError = true;
    });

    request.addEventListener("loadend", function()
    {
        if(request.isNetworkError)
        {
            console.warn(`Skipping loadend for API request ${result.id}, network error detected.`);
            return;
        }

        //Perform "always" even before we attempt to parse anything
        if(always) 
            always(result);

        if(request.status >= 200 && request.status <= 299)
        {
            if(parseData && request.responseText)
                result.result = parseData(request.responseText);
            else
                result.result = request.responseText;
            
            if(success) //WARN: No checks for no success callback set! This is because the default set in the API above is to alert!
                success(result);
        }
        else
        {
            //NOTE: Unhandled exceptions produce a special page. Furthermore, some types of validation errors
            //provided directly by ASP.NET produce error objects rather than simple strings. This makes it difficult
            //for users of the API to know exactly how to handle errors. This API interface for frontends SHOULD, someday,
            //automatically parse all these things for the users and give them easily digestible messages with a normalized
            //format, with extra data if they want to dig deeper.
            error.message = request.responseText;
            error.status_code = request.status;
            me.HandleError(handler, error);
        }
    });

    request.open(method, url);
    request.setRequestHeader("accept", "application/json");
    request.setRequestHeader("Cache-Control", "no-cache, no-store, must-revalidate");
    request.setRequestHeader("Pragma", "no-cache"); //For internet explorer?

    var token = me.get_token();

    if(token)
        request.setRequestHeader("Authorization", `Bearer ${token}`)

    if(modifyRequest)
        modifyRequest(request);

    if(postData)
    {
        //Don't modify the data if it's already in a special format
        if(postData instanceof FormData)
        {
            //request.setRequestHeader("Content-Type", "multipart/form-data");
            request.send(postData);
        }
        else
        {
            request.setRequestHeader("Content-Type", "application/json");
            request.send(JSON.stringify(postData));
        }
    }
    else
    {
        request.send();
    }
};


// ---------------------------------------------------------------------------------
// !! From here on out, these are the endpoints you will MOST LIKELY want to call !!
// ---------------------------------------------------------------------------------

Api.prototype.Login = function(loginData, handler)
{
    this.Raw("user/login", loginData, handler);
};

// registerData is "RegisterParameter"
Api.prototype.Register = function(registerData, handler)
{
    this.Raw("user/register", registerData, handler);
};

// email is literally just the email string
Api.prototype.SendRegistrationEmail = function(email, handler)
{
    this.Raw("user/sendregistrationcode", email, handler);
};

// To register, you actually need to call two endpoints, as the standard registration
// endpoint JUST reserves an account. It does not send the registration email. Call 
// this function to do both. Result passed to handler is user data, which is taken
// from the first call (register). NOTE: This function REQUIRES you pass in a handler!
Api.prototype.RegisterAndEmail = function(registerData, handler)
{
    var originalSuccess = handler.success;
    var me = this;
    var originalResult = null;

    //After first success, call second endpoint with their original success handler
    handler.success = d =>
    {
        //Only the register call (the first one) returns the user data, which you
        //probably want.
        originalResult = d;
        handler.success = dd => {
            originalSuccess(originalResult);
        };
        me.SendRegistrationEmail(registerData.email, handler);
    };

    this.Register(registerData, handler);
}

// confirmData should be a ConfirmRegistrationParameter
Api.prototype.ConfirmRegistration = function(confirmData, handler)
{
    this.Raw(`user/confirmregistration`, confirmData, handler);
};

Api.prototype.About = function(handler)
{
    this.Raw("status", null, handler);
};

// For now, this is the only way for you to know who you are. This will return an object
// with your userId that is attached to the current token, which you can then use in a 
// second request to lookup any data about yourself that you want using the standard
// "request" endpoint.
Api.prototype.AboutToken = function(handler)
{
    this.Raw("status/token", null, handler);
};

// This is your main source for all data on the website, excluding live updates.
Api.prototype.Search = function(request, handler)
{
    this.Raw("request", request, handler);
};

// This is your go-to resource to see all the available search fields, types, and the 
// format of all returned objects. NOTE: those objects are ALSO the format of the objects
// you can write to the API, so please refer to this whenever you need more information
// about anything!
Api.prototype.AboutSearch = function(handler)
{
    this.Raw("request/about", null, handler);
};

// NOTE: writes do not need your user information, because it's part of your login token.
// This API wrapper tracks and sends your login token for you, so any writes are done by
// "you". Writes will always fail if you're not logged in (or should, at least)
Api.prototype.WriteType = function(type, object, handler)
{
    this.Raw("write/" + type.replace(/[^a-zA-Z]+/g, "").toLowerCase(), object, handler);
};

// This is just a simplified shortcut for writing NEW comments. For updating existing 
// comments, just send the comment object that you pulled from the API.
// NOTES: comments are an interesting thing. Due to historical reasons, several key
// pieces of information for comment rendering is stored in the "values" dictionary
// that's part of every comment. So you'll commonly see "m", "a", and "n" values on
// comments, representing the markup, avatar, and nickname, respectively. More may be
// added in the future as well! Because they're not native fields on comments, and 
// because constructing a new comment is a bit cumbersome, this function strives to
// simplify all that by allowing you to provide the very basic fields. You don't have
// to supply the user for any writes, because it is part of your login token supplied
// by this API wrapper.
Api.prototype.WriteNewComment = function(newComment, handler)
    //text, contentId, markup, avatar, nickname, handler)
{
    var values = {};
    if(newComment.markup) values.m = newComment.markup; //Should be one of the supported markups
    if(newComment.avatar) values.a = newComment.avatar; //Should be a file HASH (a string), not the id!!
    if(newComment.nickname) values.n = newComment.nickname;

    this.WriteType(APICONST.WRITETYPES.MESSAGE, {
        text : newComment.text,
        contentId : newComment.contentId,
        values : values
    }, handler);
};

// Use this endpoint to upload files to the API. Requires login. fileUploadParam can either
// be a pre-filled FormData (if for instance you know exactly how the endpoint works), or
// it can be the UploadFileParameter object
Api.prototype.UploadFile = function(fileUploadParam, handler)
{
    //Unlike other endpoints, the file upload endpoint takes all its params as FormData!
    //This is because we need to upload the file, and this is just easier.
    var data = new FormData();

    if(fileUploadParam instanceof FormData)
    {
        data = fileUploadParam;
    }
    else
    {
        for (var k in fileUploadParam) {
            if (fileUploadParam.hasOwnProperty(k) && fileUploadParam[k] !== undefined)
                data.set(k, fileUploadParam[k]);
        }
    }

    //The api actually ignores periods specifically for this endpoint, and we need that in
    //order for this empty value to go through as form data
    if(data.get("globalPerms") === "")
    {
        console.debug("Adding hack to force empty globalPerms to send through formData for file upload");
        data.set("globalPerms", "."); 
    }

    this.Raw("file", data, handler);
};

Api.prototype.WatchPage = function(pid, handler)
{
    //Just send SOMETHING in post, it actually doesn't matter what though
    this.Raw(`shortcuts/watch/add/${pid}`, true, handler);
};

Api.prototype.UnwatchPage = function(pid, handler)
{
    //Just send SOMETHING in post, it actually doesn't matter what though
    this.Raw(`shortcuts/watch/delete/${pid}`, true, handler);
};

Api.prototype.ClearNotifications = function(pid, handler)
{
    //Just send SOMETHING in post, it actually doesn't matter what though
    this.Raw(`shortcuts/watch/clear/${pid}`, true, handler);
};

//You can ALSO get modules from the request/search endpoint, however you won't
//get the special lua data necessary to generate help data/etc. Leave name
//null to get all modules. You can exclude expensive fields if you want a faster
//search with something like "~values,votes,permissions,keywords,text"
Api.prototype.SearchModules = function(name, fields, handler)
{
    var params = new URLSearchParams();

    if(name) params.set("name", name);
    if(fields) params.set("fields", fields);

    this.Raw("module/search?" + params.toString(), undefined, handler);
};

// Get the debug logs (a list of strings) for the given module (no search options)
Api.prototype.GetModuleDebugLog = function(name, handler)
{
    this.Raw(`module/debug/${name}`, undefined, handler);
};

//You don't need to specify name because it's part of the module
Api.prototype.WriteModuleByName = function(module, handler)
{
    this.Raw("module/byname", module, handler);
};

//If you don't care about the fields, let this function pick decent defaults
Api.prototype.WriteModuleByNameEasy = function(name, code, handler)
{
    //FYI: if you don't make your module publicly readable, nobody can really
    //use your module... plus open source is good
    var module = {
        name : name,
        text : code,
        permissions : { "0" : "CR" }
    };

    this.Raw("module/byname", module, handler);
};

// EVERY module message needs a parent id to send in
Api.prototype.WriteModuleMessage = function(module, parentId, command, handler)
{
    this.Raw(`module/${module}/${parentId}`, command, handler);
};


// -------------------
//   Websocket stuff 
// -------------------

// Return a websocket instance already pointing to the appropriate endpoint
Api.prototype.GetRawWebsocket = function()
{
    var realUrl = this.ResolveRelativeUrl(this.url)
    var wsurl = realUrl.replace(/^http/, "ws") + "live/ws";
    var result = new WebSocket(wsurl);
    console.debug("Opened API websocket at: " + wsurl);
    return result;
};

// Send a request of the given type (with the given data/id) on the given websocket.
Api.prototype.SendWebsocketRequest = function(websocket, type, data, id)
{
    var me = this;
    websocket.send(JSON.stringify(new WebsocketRequest(type, data, id, me.get_token())));
};

// To make your life easier, call this function to create a websocket that will
// reconnect on failure, and to have new functions available on the websocket for
// easily making symmetric requests and handling live updates. Note that if you don't
// pass "liveUpdatesHandler", you can do it later by setting that field in the websocket
// object. Set "interceptResponseErrors" to true to have errors from requests you make go
// through your errorEvent function. Also, errors passed through "errorEvent" MAY have
// a new websocket sent along with it, representing the automatic reconnect. If you need
// to keep track of your websocket object, PLEASE listen for those!
Api.prototype.AutoWebsocket = function(liveUpdatesHandler, errorEvent, reconnectIntervalGenerator, oldWs)
{
    //TODO: ALSO TRACK LIVE UPDATES ID!!! AND HANDLE ERROR IF LIVE UPDATES CAN'T DO ANYTHING!
    reconnectIntervalGenerator = reconnectIntervalGenerator || (x => Math.min(30000, x * 500));
    var me = this;
    var ws = me.GetRawWebsocket(); 

    ws.manualCloseRequested = false;
    ws.liveUpdatesHandler = liveUpdatesHandler;
    ws.isOpen = false;

    if(oldWs)
    {
        ws.pendingRequests = oldWs.pendingRequests; // Bring over pending requests, probably while we were closed
        ws.pendingSends = oldWs.pendingSends;
        ws.currentReconnects = oldWs.currentReconnects;
        ws.liveUpdatesId = oldWs.liveUpdatesId;
    }
    else
    {
        ws.pendingRequests = {};
        ws.pendingSends = [];
        ws.currentReconnects = 0;
        ws.liveUpdatesId = 0;
    }

    var oldClose = ws.close;
    ws.close = function() 
    {
        //A one time use thing! Is this OK???
        console.log("User called websocket.close() manually");
        ws.manualCloseRequested = true;
        ws.isOpen = false; //A safety precaution
        ws.close = oldClose;
        ws.close();
    };
    ws.removePendingRequest = function(id)
    {
        if(ws.pendingRequests[id])
            delete ws.pendingRequests[id];
    };
    ws.errorEvent = function(message, response, newWs)
    {
        if(errorEvent)
            errorEvent(message, response, newWs);
        else
            console.warn(`No error handler set for websocket, got error: ${message}, response:`, response);
    };
    ws.sendRequest = function(type, data, handler)
    {
        if(!ws.isOpen)
        {
            console.debug(`Buffering request '${type}', websocket isn't open yet`);
            ws.pendingSends.push([type, data, handler]);
        }
        else
        {
            var id = String(Math.random()).substring(2);
            ws.pendingRequests[id] = handler;
            console.debug(`Sending websocket request '${type}':\"${id}\"`);
            me.SendWebsocketRequest(ws, type, data, id);
        }
    };
    ws.onopen = function()
    {
        console.debug("contentapi websocket is open!");
        ws.isOpen = true;
        ws.pendingSends.forEach(x => ws.sendRequest(x[0], x[1], x[2]));
        ws.pendingSends = [];
        ws.currentReconnects = 0;
    };
    ws.onmessage = function(event)
    {
        try
        {
            var response = JSON.parse(event.data);

            if(response.type === "live")
            {
                if(ws.liveUpdatesHandler)
                    ws.liveUpdatesHandler(response);
                else
                    console.warn("Receive live update from websocket but no handler set! Response:", response);
            }
            else if(response.type === "unexpected")
            {
                console.warn("Unexpected error from websocket: ", response);
                ws.errorEvent("Unexpected error from websocket: " + response.error, response);
                ws.close();
            }
            else if(ws.pendingRequests[response.id])
            {
                ws.pendingRequests[response.id](response);
                ws.removePendingRequest(response.id);
            }
            else
            {
                console.warn("Don't know how to handle websocket response! This is an internal API error! Response: ", response);
            }
        }
        catch(ex)
        {
            console.warn("Failed to handle message from websocket, this is an internal error!: ", event);
        }
    };
    //ws.onerror = function() { }; //ws error is almost entirely useless because of CORS
    ws.onclose = function()
    {
        //Ensure anything checking to see if we're open actually... thing.
        ws.isOpen = false;

        if(ws.manualCloseRequested)
        {
            console.debug("User requested websocket close, actually closing");
        }
        else
        {
            var reconnectInterval = reconnectIntervalGenerator(++ws.currentReconnects); //Another reconnection attempt. Only an open websocket will reset this
            console.warn(`Websocket closed unexpectedly, attempting new connection in ${reconnectInterval} ms`);
            window.setTimeout(() =>
            {
                if(ws.manualCloseRequested)
                {
                    console.warn("After unexpected websocket close, the websocket was closed manually. Reconnect attempts will halt");
                }
                else
                {
                    var newWs = me.AutoWebsocket(liveUpdatesHandler, errorEvent, reconnectIntervalGenerator, ws);
                    newWs.errorEvent("New websocket replacement after reconnect", null, newWs);
                }
            }, reconnectInterval);
        }
    };

    console.log("Successfully initialized automatic websocket (not necessarily open yet)! Use .sendRequest(type, data) to send requests! You will automatically get live update data");

    return ws;
};


// ---------------------------------------------------------------------------------
// -- Some simple, common use cases for accessing the search endpoint. --
// NOTE: You do NOT need to directly use these, especially if they don't fit your needs. 
// You can simply use them as a starting grounds for your own custom constructed searches if you want
// ---------------------------------------------------------------------------------

// Note: this could still return an empty list with "success", it's up to you to handle 
// if the list is empty
Api.prototype.Search_ById = function(type, id, handler)
{
    var search = new RequestParameter(
        { id : id }, 
        [ new RequestSearchParameter(type, "*", "id = @id") ]
    );

    this.Search(search, handler);
};

Api.prototype.Search_AllByType = function(type, fields, order, perPage, page, handler)
{
    var search = new RequestParameter({ }, 
        [ new RequestSearchParameter(type, fields || "*", "", order, perPage, page * perPage) ]
    );

    this.Search(search, handler);
};

// Retrieve a single page, along with its subpages in a special list, and all users associated with everything.
// Expects pagination, since this is just an example. NOTE: this is ONLY for pages!
Api.prototype.Search_BasicPageDisplay = function(id, subpagesPerPage, subpagePage, commentsPerPage, commentPage, handler)
{
    var search = new RequestParameter({
        pageid : id,
        filetype : 3
    }, [
        new RequestSearchParameter("content", "*", "id = @pageid"),
        //Subpages: we want most fields, but not SOME big/expensive fields. Hence ~
        new RequestSearchParameter("content", "~values,keywords,votes", "parentId = @pageid and !notdeleted() and contentType <> @filetype", "contentType,literalType,name", subpagesPerPage, subpagesPerPage * subpagePage, "subpages"),
        new RequestSearchParameter("message", "*", "contentId = @pageid and !notdeleted() and !null(module)", "id_desc", commentsPerPage, commentsPerPage * commentPage),
        // We grab your personal watches specifically for the main page to see if you ARE watching it
        new RequestSearchParameter("watch", "*", "contentId = @pageid"),
        new RequestSearchParameter("user", "*", "id in @message.createUserId or id in @content.createUserId or id in @subpages.createUserId"),
    ]);

    this.Search(search, handler);
};

// Get a list of your personal notification data + related content
Api.prototype.Notifications = function(contentPerPage, page, handler)
{
    var search = new RequestParameter({ }, [
        new RequestSearchParameter("watch", "*", "", "commentNotifications_desc,activityNotifications_desc", contentPerPage, contentPerPage * page), //all your personal watches
        new RequestSearchParameter("content", "~values,keywords,votes", "id in @watch.contentId"),
        new RequestSearchParameter("user", "*", "id in @content.createUserId"),
    ]);

    this.Search(search, handler);
};

// Return a list of module messages PRE-linked to users. All parameters are optional.
Api.prototype.GetModuleMessages = function(pageId, module, limit, handler)
{
    var moduleSearch = new RequestSearchParameter("message", "*", "!notnull(module)","id");
    var values = {};

    if(limit) moduleSearch.limit = limit;
    if(module) 
    { 
        moduleSearch.query += " and module=@name";
        values.name = module;
    }
    if(pageId)
    {
        moduleSearch.query += " and contentId=@cid";
        values.cid = pageId;
    }

    var search = new RequestParameter(values, [
        moduleSearch,
        new RequestSearchParameter("user", "*", "id in @message.uidsInText or id in @message.createUserId or id in @message.receiveUserId")
    ]);

    this.Search(search, handler); 
};

// -- Some helper functions which don't necessarily directly connect to the API --

// Return the URL for a file based on its public hash. This accepts just the hash
// because that is what's used for file links. "modify" is FileModifyParameter
Api.prototype.GetFileUrl = function(hash, modify)
{
    var params = new URLSearchParams();

    for(var k in modify)
    {
        if(modify.hasOwnProperty(k) && modify[k])
            params.set(k, modify[k]);
    }

    var paramString = params.toString();

    var url = this.url + `file/raw/${hash}`;

    if(paramString.length)
        url += "?" + paramString;

    return encodeURI(url);
};

// Return a dictionary where each key is the id from the given dataset
Api.prototype.KeyById = function(data, idField)
{
    idField = idField || "id";
    var result = {};
    data.forEach(x => { result[x[idField]] = x; });
    return result;
};

// Given a dictionary of link ID fields to linked field names, automatically link the 
// given data using the given list. This means that your "data" might be, for instance,
// a list of comments, and the "list" is a list of users, and your "autoLinks" is a 
// dictionary with stuff like "createUserId" and "editUserId". This function will add 
// extra fields to each comment that points back to the real user object for any field
// in "autoLinks". For an example, see the specific "AutoLink" functions after this
Api.prototype.AutoLinkGeneric = function(data, list, autoLinks)
{
    var linkItems = this.KeyById(list);
    data.forEach(x =>
    {
        for(var k in autoLinks)
        {
            var link = x[k];

            //Link the found user to the field specified as the value in the userAutoLinks field
            if(link && linkItems[link])
                x[autoLinks[k]] = linkItems[link];
        }
    });
};

// Automatically link users to the given dataset. Tries to find normal fields like "createUserId",
// and adds an additional field "createUser" which is the full user data from the provided userlist.
// WARN: THIS MODIFIES THE DATASET IN-PLACE!
Api.prototype.AutoLinkUsers = function(data, userlist)
{
    this.AutoLinkGeneric(data, userlist, this.userAutolinks);
};

// Automatically link content to the given dataset (even if the data is also content, such as linking
// for parentId). Tries to find normal fields like "contentId" and "parentId" and adds an additional
// field like "content" and "parent", which is the full content data from the provided contentlist.
// WARN: THIS MODIFIES THE DATASET IN-PLACE!
Api.prototype.AutoLinkContent = function(data, contentList)
{
    this.AutoLinkGeneric(data, contentList, this.contentAutolinks);
};

// Given one of the type descriptions from the "details" resultset of "AboutSearch", returns
// all the fields which can be used in a query (the SQL-like query field of requests)
Api.prototype.GetQueryableFields = function(typeDescriptor)
{
    var result = [];

    for(var k in typeDescriptor)
    {
        if(typeDescriptor[k].queryable)
            result.push(k);
    }

    return result;
};

// Given one of the type descriptions from the "details" resultset of "AboutSearch", returns
// all the fields which can be retrieved in the "fields" list of a request
Api.prototype.GetRequestableFields = function(typeDescriptor)
{
    return Object.keys(typeDescriptor);
};

// Fill the given container (hopefully a select element) with options for our supported markup
Api.prototype.FillMarkupSelect = function(selector, modify)
{
    APICONST.MARKUP.forEach(x =>
    {
        var opt = document.createElement("option");
        opt.textContent = x;
        if(modify) modify(opt);
        selector.appendChild(opt);
    });
};

Api.prototype.ResolveRelativeUrl = function(url)
{
    var link = document.createElement("a");
    link.href = url;
    return link.protocol+"//"+link.host+link.pathname+link.search+link.hash;
};