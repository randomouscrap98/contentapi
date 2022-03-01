// haloopdy - 01/2022
// The script for the ultra-simple frontend implementation meant to serve as a simple example
// for other frontend designers on how to consume the API.

var api;

const SUBPAGESPERPAGE = 100;
const COMMENTSPERPAGE = 100;
const SEARCHRESULTSPERPAGE = 100;
const TINYAVATAR = 25;

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


// -- General utilities (for this page) --

//Convert "page state" to a url. This frontend is very basic!
function StateToUrl(state)
{
    var params = new URLSearchParams();

    for(var k in state)
    {
        if(state.hasOwnProperty(k) && state[k])
            params.set(k, state[k]);
    }

    return "?" + params.toString();
}

//Make a "deep copy" of the given object (sort of)
function Copy(object) { return JSON.parse(JSON.stringify(object)); }

// For simplicity, convert an object into something editable in a single simple textbox
function QuickObjectToInput(object)
{
    var valueStr = JSON.stringify(object);
    return valueStr.substring(1, valueStr.length - 1);
}

// For simplicity, the opposite of above (take a single simple textbox and convert to object)
function QuickInputToObject(value)
{
    if(value) return JSON.parse("{" + value + "}");
    else return {};
}

// Essentially a spoiler maker, useful for collapsible elements. The button is the control for showing or hiding
// the cointainer, and the visibleState is the initial visibility (set to false to hide initially). Note: the
// button's text content is overwritten for this.
function SetCollapseButton(button, container, visibleState)
{
    var toggle = function(forceVisibleState)
    {
        var vstate = forceVisibleState !== undefined ? forceVisibleState : container.hasAttribute("hidden"); //className.indexOf("hidden") >= 0;

        if(vstate) //If it's supposed to be visible, this
        {
            if(container.hasAttribute("hidden"))
                container.removeAttribute("hidden");
            button.textContent = "-";
        }
        else //If it's supposed to be hidden (not visible), this
        {
            container.setAttribute("hidden", "")
            button.textContent = "+";
        }
    };

    button.onclick = function() { toggle() };

    toggle(visibleState);
}


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

// Using the given list, fill the given datalist element
function FillOptions(list, datalist)
{
    datalist.innerHTML = "";
    list.forEach(x =>
    {
        var option = document.createElement("option");
        option.textContent = x;
        datalist.appendChild(option);
    });
}

//Place a url into the given link with the given state modified. Does NOT modify the 
//actual state given, that's the point of this function!
function LinkState(link, state, modify)
{
    var newState = Copy(state);
    modify(newState);
    link.href = StateToUrl(newState);
}

//Set up pagination, with the given page up and page down elements
function SetupPagination(linkUp, linkDown, state, field)
{
    //Fix state immediately, it's fine.
    state[field] = Math.max(0, state[field] || 0);

    if(state[field] > 0)
        LinkState(linkDown, state, x => x[field]--);

    LinkState(linkUp, state, x => x[field]++);
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
    SetupPagination(template.querySelector("#user-files-pageup"), template.querySelector("#user-files-pagedown"), state, "fp");
    SetCollapseButton(template.querySelector("#user-update-toggle"), template.querySelector("#user-update-form"), false);
    SetCollapseButton(template.querySelector("#user-file-upload-toggle"), template.querySelector("#user-file-upload"), false);

    var table = template.querySelector("#user-table");
    var avatar = template.querySelector("#user-avatar");
    var userFiles = template.querySelector("#user-files-container");

    api.AboutToken(new ApiHandler(d =>
    {
        var search = new RequestParameter({
            uid : d.result.userId
        }, [
            new RequestSearchParameter("user", "*", "id = @uid"),
            new RequestSearchParameter("file", "*", "createUserId = @uid", "id_desc", SEARCHRESULTSPERPAGE, state.fp * SEARCHRESULTSPERPAGE),
        ]);

        api.Search(search, new ApiHandler(dd =>
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

            var user = dd.result.data.user[0];
            avatar.src = api.GetFileUrl(user.avatar, new FileModifyParameter(50));
            MakeTable(user, table);

            template.querySelector("#user-update-id").value = user.id;
            template.querySelector("#user-update-username").value = user.username;
            template.querySelector("#user-update-avatar").value = user.avatar;
            template.querySelector("#user-update-special").value = user.special;
            template.querySelector("#user-update-groups").value = user.groups.join(" ");
            template.querySelector("#user-update-super").value = user.super;

            //Now go set up the file list display thing
            dd.result.data.file.forEach(x => {
                var item = LoadTemplate(`file_item`, x);
                userFiles.appendChild(item);
            });
        }));
    }));
}

function page_onload(template, state)
{
    state.pid = Number(state.pid || 0);

    SetupPagination(template.querySelector("#page-subpageup"), template.querySelector("#page-subpagedown"), state, "sp");
    SetupPagination(template.querySelector("#page-commentup"), template.querySelector("#page-commentdown"), state, "cp");

    var table = template.querySelector("#page-table");
    var content = template.querySelector("#page-content");
    var title = template.querySelector("#page-title");
    var subpagesElement = template.querySelector("#page-subpages");
    var commentsElement = template.querySelector("#page-comments");

    var setupEditor = function(type, page)
    {
        //Need to load the page editor! Since it's a "new" editor, the pid is 0
        var pageEditor = LoadTemplate("page_editor", page);
        var editorContainer = template.querySelector(`#page-${type}-container`);
        SetCollapseButton(template.querySelector(`#page-${type}-toggle`), editorContainer, false);
        editorContainer.appendChild(pageEditor);
    };

    setupEditor("submit", {parentId : state.pid, id : 0});

    //Setup the comment submit stuff if we're not at root, otherwise hide comment submit
    if(state.pid)
    {
        api.FillMarkupSelect(template.querySelector("#comment-submit-markup"));
        template.querySelector("#comment-submit-contentid").value = state.pid;
    }
    else
    {
        template.querySelector("#comment-submit-container").setAttribute("hidden", ""); 
        template.querySelector("#page-edit-section").setAttribute("hidden", ""); 
    }

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
            var originalPage = JSON.parse(JSON.stringify(page));
            title.textContent = page.name;
            content.textContent = page.content;
            delete page.name;
            delete page.content;
            page.votes = JSON.stringify(page.votes);
            page.values = JSON.stringify(page.values);
            page.keywords = JSON.stringify(page.keywords);
            page.permissions = JSON.stringify(page.permissions);
            MakeTable(page, table);

            setupEditor("edit", originalPage);
        }

        //Waste a few cycles linking some stuff together!
        api.AutoLinkUsers(d.result.data.subpages, d.result.data.user);
        api.AutoLinkUsers(d.result.data.comment, d.result.data.user);

        d.result.data.subpages.forEach(x => {
            var subpage = LoadTemplate("page_item", x);
            subpagesElement.appendChild(subpage);
        });

        d.result.data.comment.forEach(x => {
            var comment = LoadTemplate("comment_item", x);
            commentsElement.appendChild(comment);
        });
    }));
}

function search_onload(template, state)
{
    SetupPagination(template.querySelector("#search-up"), template.querySelector("#search-down"), state, "sp");

    //Do some initial set up. Fill in some of the fields and such
    var searchtext = template.querySelector("#search-text")
    var searchtype = template.querySelector("#search-type")
    var searchfield = template.querySelector("#search-field");
    var searchsort = template.querySelector("#search-sort");

    var resultElement = template.querySelector("#search-results");
    var aboutElement = template.querySelector("#search-about");

    searchtext.value = state.search || "";
    searchtype.value = state.type || "content";

    //Also, go out and get the "about" information so we can fill in the datalist elements for options
    api.AboutSearch(new ApiHandler(d => 
    {
        //Assume the format is known, and the data will be fine.
        var resetOptions = function()
        {
            console.log(searchtype.value, d);
            FillOptions(api.GetQueryableFields(d.result.details.types[searchtype.value]), searchfield);
            var sortOptions = [];
            api.GetRequestableFields(d.result.details.types[searchtype.value]).forEach(x =>
            {
                sortOptions.push(x);
                sortOptions.push(x + "_desc");
            });
            FillOptions(sortOptions, searchsort);
        };
        searchtype.oninput = resetOptions;
        resetOptions();

        //Now that the options have been reset based on the search criteria, reset the junk
        searchfield.value = state.field || "";
        searchsort.value = state.sort || "";
    }));

    //If a type was at least given, we can perform a search (probably). remember, this onload function is both for loading
    //the search page state AND performing the search, since oldschool forms just submit the data to the same page.
    if(state.type)
    {
        //This is how you'd set up your own search
        var values = {};
        var query = "";

        if(state.search)
        {
            values.search = `%${state.search}%`;
            query = `${state.field} LIKE @search`;
        }

        var requests = [
            new RequestSearchParameter(state.type, "*", query, state.sort, SEARCHRESULTSPERPAGE, state.sp * SEARCHRESULTSPERPAGE, "main"),
        ];

        //Can't link users to themselves... not really
        if(state.type != "user")
            requests.push(new RequestSearchParameter("user", "*", "id in @main.createUserId", ""));

        var search = new RequestParameter(values, requests);

        api.Search(search, new ApiHandler(d =>
        {
            //If we DID ask for the users, link them
            if(d.result.data.user)
                api.AutoLinkUsers(d.result.data.main, d.result.data.user);

            d.result.data.main.forEach(x => {
                var item = LoadTemplate(`${state.type}_item`, x);
                resultElement.appendChild(item);
            });

            aboutElement.textContent = `Search took ${d.result.totalTime} ms`;
        }));
    }
}

// -- Loaders, but not for pages, just for little templates--

function page_item_onload(template, state)
{
    //Set up the subpage item on load
    var type = template.querySelector("[data-type]");
    var title = template.querySelector("[data-title]");
    var time = template.querySelector("[data-time]");
    type.textContent = state.type;
    title.href = "?t=page&pid=" + state.id;
    title.textContent = state.name;
    time.textContent = state.createDate;
}

function comment_item_onload(template, state)
{
    var avatar = template.querySelector("[data-avatar]");
    var username = template.querySelector("[data-username]");
    var comment = template.querySelector("[data-comment]");
    var time = template.querySelector("[data-time]");
    var contentid = template.querySelector("[data-contentid]");

    if(state.createUser)
    {
        avatar.src = api.GetFileUrl(state.createUser.avatar, new FileModifyParameter(TINYAVATAR, true));
        username.textContent = state.createUser.username;
    }
    else
    {
        username.textContent = "???";
    }
    username.title = state.createUserId;
    comment.textContent = state.text;
    comment.title = state.id;
    time.textContent = state.createDate;
    contentid.textContent = `(${state.contentId})`;
}

function user_item_onload(template, state)
{
    var avatar = template.querySelector("[data-avatar]");
    var username = template.querySelector("[data-username]");
    var time = template.querySelector("[data-time]");
    var sup = template.querySelector("[data-super]");

    avatar.src = api.GetFileUrl(state.avatar, new FileModifyParameter(TINYAVATAR, true));
    username.textContent = state.username;
    username.title = state.id;
    time.textContent = state.createDate;

    if(!state.super)
        sup.style.display = "none";
}

function file_item_onload(template, state)
{
    var file = template.querySelector("[data-file]");
    var filelink = template.querySelector("[data-filelink]");
    var hash = template.querySelector("[data-hash]");
    var time = template.querySelector("[data-time]");
    var private = template.querySelector("[data-private]");

    file.src = api.GetFileUrl(state.hash, new FileModifyParameter(50));
    file.title = `${state.mimetype} : ${state.quantization}`;
    filelink.href = api.GetFileUrl(state.hash);
    hash.textContent = `${state.name} [${state.id}] pubId: ${state.hash}`;
    time.textContent = state.createDate;

    if(state.permissions[0] && state.permissions[0].indexOf("R") >= 0)
        private.style.display = "none";
}

function page_editor_onload(template, state)
{
    //state is going to be the page IN the format from the api itself.
    state = state || {};
    template.querySelector("#page-editor-id").value = state.id || 0;
    template.querySelector("#page-editor-parentid").value = state.parentId || 0;
    template.querySelector("#page-editor-name").value = state.name || "";
    template.querySelector("#page-editor-text").value = state.text || "";
    template.querySelector("#page-editor-type").value = state.type || "";

    if(state.keywords)
        template.querySelector("#page-editor-keywords").value = state.keywords.join(" ");
    if(state.values)
        template.querySelector("#page-editor-values").value = QuickObjectToInput(state.values);
    if(state.permissions)
        template.querySelector("#page-editor-permissions").value = QuickObjectToInput(state.permissions);
}


// -- Functions templates use directly (mostly form submits) --

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

function t_comment_submit_submit(form)
{
    var text = document.getElementById("comment-submit-text").value;
    var markup = document.getElementById("comment-submit-markup").value;
    var contentId = document.getElementById("comment-submit-contentid").value;

    //NOTE: if you want the avatar you used to comment with saved with the comment for posterity
    //(meaning searching for your old comment will show your original avatar when commenting and not
    // your current avatar), you can add your avatar to the metadata. 
    api.WriteNewComment(new NewCommentParameter(text, contentId, markup), new ApiHandler(d => {
        location.reload();
    }));

    return false;
}

function t_page_editor_submit(form)
{
    //Pull the form together. These are about all the actual values you'd write for any page!
    //It just looks complicated in real frontends because the "values" array probably contains
    //things you want individual inputs for, like "what's the key" or whatever
    var page = {
        id : Number(form.querySelector("#page-editor-id").value),
        contentType : Number(form.querySelector("#page-editor-contenttype").value),
        parentId : Number(form.querySelector("#page-editor-parentid").value),
        name : form.querySelector("#page-editor-name").value,
        text : form.querySelector("#page-editor-text").value,
        type : form.querySelector("#page-editor-type").value,
        keywords : form.querySelector("#page-editor-keywords").value.split(" "),
        permissions : QuickInputToObject(form.querySelector("#page-editor-permissions").value),
        values : QuickInputToObject(form.querySelector("#page-editor-values").value)
    };

    api.WriteType(APICONST.WRITETYPES.CONTENT, page, new ApiHandler(d => {
        location.href = "?t=page&pid=" + d.result.id;
    }));

    return false;
}

function t_user_files_submit(form)
{
    var data = new FormData(form);

    //We set up our form to be EXACTLY the form data that is required, so just... do that.
    api.UploadFile(data, new ApiHandler(d => {
        alert("Upload successful. ID: " + d.result.id);
        console.log("Upload successful. ID: " + d.result.id);
        location.reload();
    }));

    return false;
}

function t_user_update_submit(form)
{
    //Pull the form together. These are about all the actual values you'd write for any page!
    //It just looks complicated in real frontends because the "values" array probably contains
    //things you want individual inputs for, like "what's the key" or whatever
    var user = {
        id : Number(form.querySelector("#user-update-id").value),
        username : form.querySelector("#user-update-username").value,
        avatar : form.querySelector("#user-update-avatar").value,
        special : form.querySelector("#user-update-special").value,
        groups : form.querySelector("#user-update-groups").value.split(" ").filter(x => x).map(x => Number(x)),
        super : Number(form.querySelector("#user-update-super").value)
    };

    api.WriteType(APICONST.WRITETYPES.USER, user, new ApiHandler(d => {
        location.reload();
    }));

    return false;
}