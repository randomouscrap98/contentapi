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
    }
};

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

//An object which tells how to handle an API call, whether it succeeded or failed
function ApiHandler(success, error)
{
    this.always = false;                //What to "always" do, regardless of failure or not. "Always" is called at the start of any api response, if provided
    this.success = success || false;    //What to do when the call was successful. Depending on the call, you might get raw data or parsed
    this.error = error || { };          //This might seem strange, but the error handler is actually a dictionary. 0 is the default handler, any other code is the handler for that code
    //this.error = false;             //The generic error handler. If no other handlers caught the error first, it goes here.
    //this.unknown_error = false;     //Unknown errors are ones we aren't equipped to handle!
    //this.auth_error = false;        //Authentication errors are when you need a token but you didn't give one
    //this.token_error = false;       //Token errors are when you gave a token but it was rejected
    //this.permission_error = false;  //Permission errors are when you're not allowed to view the given content
    //this.not_found_error = false;   //Not found errors are when you requested something that doesn't exist!
}

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
            alert("No default error handler set for api!");
            console.log("Error from API: ", e);
        }
    );
}

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
    parseData = parseData || (x => x);
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
    });

    request.addEventListener("loadend", function()
    {
        //Perform "always" even before we attempt to parse anything
        if(always) 
            always(result);

        if(request.status >= 200 && request.status <= 299)
        {
            if(parseData)
                result.result = parseData(request.responseText);
            else
                result.result = request.responseText;
            
            if(success) //WARN: No checks for no success callback set! This is because the default set in the API above is to alert!
                success(result);
        }
        else
        {
            error.message = "Some error"; //TODO: Fix this
            error.status_code = request.status;
            me.HandleError(handler, error);
        }
    });

    request.open(method, url);
    request.setRequestHeader("accept", "application/json");
    request.setRequestHeader("Content-Type", "application/json");
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
            request.send(postData);
        else
            request.send(JSON.stringify(postData));
    }
    else
    {
        request.send();
    }
};