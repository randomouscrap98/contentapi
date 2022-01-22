// haloopdy - 01/2022
// The script for the ultra-simple frontend implementation meant to serve as a simple example
// for other frontend designers on how to consume the API.

//NOTE: although this is set up to use templates and dynamic loading as an example, this is NOT
//SPA. It does not attempt to intercept URLS and do all the fanciness required for that.
window.onload = function()
{
    //NOTE: there is no error checking in this frontend, because it is not meant to be used full time. Furthermore,
    //I think it would distract from the ideas I'm trying to convey, which is just how the API is used to make
    //a functioning website.

    var parameters = new URLSearchParams(location.search);
    var state = Object.fromEntries(parameters);
    state.api = new Api(null, GetToken); //Null just means use the default API endpoint. You can provide another URL instead

    //Load a template! Otherwise, just leave the page as-is
    if(parameters.has("t"))
    {
        LoadPageStandard(parameters.get("t"), state);
    }
};


// -- Getters and setters for stateful (cross page load) stuff --

const TOKENKEY = "contentapi_defimpl_userkey";
function GetToken() { localStorage.getItem(TOKENKEY); }
function SetToken(token) { localStorage.setItem(TOKENKEY, token); }


// -- Some basic templating functions --

function LoadPageInto(baseTemplate, destination, state)
{
    var template = baseTemplate.cloneNode(true);

    //If a cloning function function is found, run it. The cloning function will probably set up
    //specific page values or whatever. 
    if(template.hasAttribute("data-onload"))
        window[template.getAttribute("data-onclone")](template, state);

    destination.innerHTML = "";
    destination.appendChild(template);
}

//A wrapper for above that does the standard procedure
function LoadPageStandard(templateId, state)
{
    var main = document.getElementById("main");
    var baseTemplate = document.getElementById("templates").content.getElementById(`t_${templateId}`);
    LoadPageInto(baseTemplate, main, state);
}

// -- The individual page setup functions --

function User_OnLoad(template, state)
{

}

// -- Functions templates use directly --

function t_login_submit(form)
{
    console.log("Trying to submit:", form);
    return false;
}