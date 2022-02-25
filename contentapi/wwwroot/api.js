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
        COMMENT : "comment",
        PAGE : "page",
        FILE : "file",
        MODULE : "module",
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
    this.contentAutoLinks = {
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
            if(parseData)
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

// !! From here on out, these are the endpoints you will MOST LIKELY want to call !!

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
    this.Raw("/write/" + type.replace(/[^a-zA-Z]+/g, "").toLowerCase(), object, handler);
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

    this.WriteType(APICONST.WRITETYPES.COMMENT, {
        text = newComment.text,
        contentId = newComment.contentId,
        values = values
    }, handler);
};

// -- Some simple, common use cases for accessing the search endpoint. --
// NOTE: You do NOT need to directly use these, especially if they don't fit your needs. 
// You can simply use them as a starting grounds for your own custom constructed searches if you want

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
        pageid : id
    }, [
        new RequestSearchParameter("page", "*", "id = @pageid"),
        //Subpages: we want most fields, but not SOME big/expensive fields. Hence ~
        new RequestSearchParameter("page", "~permissions,values,keywords,votes", "parentId = @pageid", "type,name", subpagesPerPage, subpagesPerPage * subpagePage, "subpages"),
        new RequestSearchParameter("comment", "*", "contentId = @pageid and !notdeleted()", "id_desc", commentsPerPage, commentsPerPage * commentPage),
        new RequestSearchParameter("user", "*", "id in @comment.createUserId or id in @page.createUserId or id in @subpages.createUserId"),
    ]);

    this.Search(search, handler);
};

        //Some funny quirks in the API (that are a result of user requests): ALL content is "content" now, 
        //including pages, files, modules, etc. But searching against the bare minimum "content" means
        //you get only the fields that are common to all content types. This is a LOT of fields, but not 
        //enough to actually display a full page, so in order to make the search work for ANY type and
        //get ALL data, we must (currently) just ask for all types. This may be fixed in the future with
        //a further specialized request type, but for now this is what we have. It is NOT inefficient, 
        //searching for a non-existent page takes nearly no time.
        //new RequestSearchParameter("user", "*", "id in @comment.createUserId or id in @page.createUserId or id in @module.createUserId or id in @file.createUserId or id in @subpages.createUserId"),


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