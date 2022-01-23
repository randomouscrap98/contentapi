// haloopdy - 01/2022
// The script for the ultra-simple frontend implementation meant to serve as a simple example
// for other frontend designers on how to consume the API.

var api;

const SUBPAGESPERPAGE = 100;
const COMMENTSPERPAGE = 100;

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

//Convert "page state" to a url. This frontend is very basic!
function StateToUrl(state)
{
    var params = new URLSearchParams();

    for(var k in state)
    {
        if(state.hasOwnProperty(k) && state[k])
            params.set(k, state[k]);
    }

    return params;
}

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

function page_onload(template, state)
{
    //Fix up the pages just in case?
    state.sp = Math.max(0, state.sp || 0);
    state.cp = Math.max(0, state.cp || 0);
    state.pid = state.pid || 0;

    var table = template.querySelector("#page-table");
    var content = template.querySelector("#page-content");
    var title = template.querySelector("#page-title");
    var subpagesElement = template.querySelector("#page-subpages");
    var commentsElement = template.querySelector("#page-comments");

    api.Search_BasicPageDisplay(state.pid, SUBPAGESPERPAGE, state.sp, COMMENTSPERPAGE, state.cp, new ApiHandler(d =>
    {
        if(d.result.data.page.length == 0)
        {
            if(state.pid == 0)
                title.textContent = "Root parent (not a page)";
            else
                title.textContent = "Unknown page / root";
        }
        else
        {
            var page = d.result.data.page[0];
            title.textContent = page.name;
            content.textContent = page.content;
            delete page.name;
            delete page.content;
            page.votes = JSON.stringify(page.votes);
            page.values = JSON.stringify(page.values);
            page.keywords = JSON.stringify(page.keywords);
            page.permissions = JSON.stringify(page.permissions);
            MakeTable(page, table);
        }

        //Waste a few cycles linking some stuff together!
        api.AutoLinkUsers(d.result.data.subpages, d.result.data.user);
        api.AutoLinkUsers(d.result.data.comment, d.result.data.user);

        d.result.data.subpages.forEach(x => {
            var subpage = LoadTemplate("subpage_item", x);
            subpagesElement.appendChild(subpage);
        });

        d.result.data.comment.forEach(x => {
            var comment = LoadTemplate("comment", x);
            commentsElement.appendChild(comment);
        });
    }));
}

// -- Loaders, but not for pages, just for little templates--

function subpage_item_onload(template, state)
{
    //Set up the subpage item on load
    var type = template.querySelector("[data-type]");
    var title = template.querySelector("[data-title]");
    type.textContent = state.type;
    title.href = "?t=page&pid=" + state.id;
    title.textContent = state.name;
}

function comment_onload(template, state)
{
    var avatar = template.querySelector("[data-avatar]");
    var username = template.querySelector("[data-username]");
    var comment = template.querySelector("[data-comment]");
    var time = template.querySelector("[data-time]");

    avatar.src = api.GetFileUrl(state.createUser.avatar, new FileModifyParameter(25, true));
    username.textContent = state.createUser.username;
    username.title = state.createUserId;
    comment.textContent = state.text;
    time.textContent = state.createDate;
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