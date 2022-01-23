// haloopdy - 01/2022
// The script for the ultra-simple frontend implementation meant to serve as a simple example
// for other frontend designers on how to consume the API.

var api;

//NOTE: although this is set up to use templates and dynamic loading as an example, this is NOT
//SPA. It does not attempt to intercept URLS and do all the fanciness required for that.
window.onload = function()
{
    //NOTE: there is no error checking in this frontend, because it is not meant to be used full time. Furthermore,
    //I think it would distract from the ideas I'm trying to convey, which is just how the API is used to make
    //a functioning website.

    var parameters = new URLSearchParams(location.search);
    var state = Object.fromEntries(parameters);
    api = new Api(null, GetToken); //Just a global api object, whatever. Null means use the default endpoint (local to self)
    api.default_handler.error = e =>
    {
        alert(`Error ${e.status_code}: ${e.message}`);
        console.log("Error: ", e);
    };

    //Load a template! Otherwise, just leave the page as-is
    if(parameters.has("t"))
    {
        LoadPage(parameters.get("t"), state);
    }
};


// -- Getters and setters for stateful (cross page load) stuff --

const TOKENKEY = "contentapi_defimpl_userkey";
function GetToken() { return localStorage.getItem(TOKENKEY); }
function SetToken(token) { localStorage.setItem(TOKENKEY, token); }


// -- Some basic templating functions --

// Our templates are all stored in a particular place, this loads them from that place
// by default. id is assumed to be the literal id for the template within the 
// template container.
function LoadTemplate(id, state, templates)
{
    templates = templates || document.getElementById("templates").content;
    var baseTemplate = templates.getElementById(id);
    var template = baseTemplate.cloneNode(true);
    template.id = ""; //remove the id placed in the template

    //If a cloning function function is found, run it. The cloning function will probably set up
    //specific page values or whatever. 
    if(template.hasAttribute("data-onload"))
        window[template.getAttribute("data-onload")](template, state);
    
    return template;
}

// Our pages are all loaded in a standard fashion, this does all the work of loading a template
// into the given destination (by default, the main page area)
function LoadPage(id, state, destination)
{
    destination = destination || document.getElementById("main");
    var template = LoadTemplate(`t_${id}`, state);
    destination.innerHTML = "";
    destination.appendChild(template);
}

// Display the given data inside the given table element
function MakeTable(data, table)
{
    for(var p in data)
    {
        var row = document.createElement("tr");
        var name = document.createElement("td");
        var value = document.createElement("td");
        name.textContent = p;
        value.textContent = data[p];
        row.appendChild(name);
        row.append(value);
        table.appendChild(row);
    }
}


// -- The individual page setup functions --

function confirmregister_onload(template, state)
{
    //It's not in the page yet, so need to use queryselector
    var userEmail = template.querySelector("#confirmregister-email");
    userEmail.value = state.email;
}

function user_onload(template, state)
{
    var table = template.querySelector("#user-table");
    var avatar = template.querySelector("#user-avatar");

    api.AboutToken(new ApiHandler(d =>
    {
        api.Search_UserId(d.result.userId, new ApiHandler(dd =>
        {
            //So the data from the api is in "result", but results from the "request"
            //endpoint are complicated and contain additional information about the request,
            //so you have to look into "data", and because request can get ANY data from the 
            //database you want, you have to go into "user" because that's what you asked for.
            if(dd.result.data.user.length == 0)
            {
                alert("No user data found!");
                return;
            }

            avatar.src = api.GetFileUrl(dd.result.data.user[0].avatar, new FileModifyParameter(50));
            MakeTable(dd.result.data.user[0], table);
        }));
    }));
}


// -- Functions templates use directly --

function t_login_submit(form)
{
    var username = document.getElementById("login-username").value;
    var password = document.getElementById("login-password").value;

    api.Login(new LoginParameter(username, password), new ApiHandler(d => {
        SetToken(d.result);
        location.href = "?t=user";
    }));

    return false;
}

function t_register_submit(form)
{
    var username = document.getElementById("register-username").value;
    var email = document.getElementById("register-email").value;
    var password = document.getElementById("register-password").value;

    api.RegisterAndEmail(new RegisterParameter(username, email, password), new ApiHandler(d => {
        location.href = `?t=confirmregister&email=${email}`;
    }));

    return false;
}

function t_confirmregister_submit(form)
{
    var email = document.getElementById("confirmregister-email").value;
    var key = document.getElementById("confirmregister-key").value;

    api.ConfirmRegistration(new ConfirmRegistrationParameter(email, key), new ApiHandler(d => {
        SetToken(d.result); //This endpoint returns a user token as well, like login!
        location.href = `?t=user`;
    }));

    return false;
}

function t_user_logout()
{
    SetToken(null);
    location.href = "?"; //Home page maybe?
}