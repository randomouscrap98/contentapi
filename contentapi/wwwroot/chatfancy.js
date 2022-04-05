//NOTE: Nothing in this file is used to make the chat work! Stick to chat.html, that has everything you really need!
//I just wanted to make the chat a little nicer, think of this as a "plugin" if you will

var chat_fancy_themes = [ "base", "dark", "purple", "blue", "frog", "terminal" ];

const FANCYSETTINGSKEY = "contentapi_defimpl_fancy";
function GetFancySettings() 
{ 
    var raw = localStorage.getItem(FANCYSETTINGSKEY); 
    return raw ? JSON.parse(raw) : {};
}
function SetFancySettings(settings) 
{ 
    localStorage.setItem(FANCYSETTINGSKEY, JSON.stringify(settings)); 
}
function SetFancySettingValue(key, value)
{
    var settings = GetFancySettings();
    settings[key] = value;
    SetFancySettings(settings);
}

window.addEventListener('load', function()
{
    var themeselectcontainer = createThemeSelector();
    chatcontrols.insertBefore(themeselectcontainer, autoscrollcontainer);
    var themeselect = document.getElementById("themeselect");

    var selectShow = createMessageSelectShow();
    chatcontrols.insertBefore(selectShow, autoscrollcontainer);

    var settings = GetFancySettings();

    themeselect.oninput = () => updateTheme(themeselect);

    if(settings.theme)
    {
        themeselect.value = settings.theme;
        updateTheme(themeselect);
    }

    var oldCreateMessage = createMessage;
    createMessage = (m) =>
    {
        var container = oldCreateMessage(m);

        if(!m.module)
        {
            var userArea = container.querySelector(".userinfo");
            var deleteButton = container.querySelector(".delete");

            var checkBox = document.createElement("input");
            checkBox.setAttribute("type", "checkbox");
            checkBox.className = "messageselect";
            userArea.insertBefore(checkBox, deleteButton);
        }

        return container;
    }
});

function updateTheme(themeselect)
{
    document.body.setAttribute('data-theme', themeselect.value);
    SetFancySettingValue("theme", themeselect.value);
}

function createLabelGeneric(text, element, textBeforeElement)
{
    var div = document.createElement('div');
    var container = document.createElement("label");
    var span = document.createElement("span");
    span.textContent = text;
    if(textBeforeElement) {
        container.appendChild(span);
        container.appendChild(element);
    }
    else {
        container.appendChild(element);
        container.appendChild(span);
    }
    div.appendChild(container);
    return div;
}

function createThemeSelector()
{
    var sel = document.createElement("select");
    sel.id = "themeselect";

    chat_fancy_themes.forEach(t =>
    {
        var option = document.createElement('option');
        option.textContent = t;
        sel.appendChild(option);
    });

    return createLabelGeneric('Theme: ', sel, true);
}

function createMessageSelectShow()
{
    var checkbox = document.createElement("input");
    var attr = 'data-showmessageselect';
    checkbox.setAttribute("type", "checkbox");
    checkbox.oninput = () =>
    {
        if(checkbox.checked)
            document.body.setAttribute(attr, "");
        else if (document.body.hasAttribute(attr))
            document.body.removeAttribute(attr);
    };
    var div = createLabelGeneric("", checkbox);
    var rethreader = createRethreader();
    div.appendChild(rethreader);
    return div;
}

function createRethreader()
{
    var div = document.createElement("span")
    var content = document.createElement("input");
    content.setAttribute('placeholder', "pid");
    content.style.width = "3em";
    var button = document.createElement('button');
    button.textContent = "Rethread";
    button.onclick = () =>
    {
        //Go find messages that are checked
        var messages = getAllMessageElements();
        messages = messages.filter(x => {
            var sel = x.querySelector('.messageselect');
            return sel && sel.checked
        });
        var ids = messages.map(y => Number(y.getAttribute("data-id")));
        if(confirm(`Do you want to rethread ${ids.length} messages: ${JSON.stringify(ids)} into content ${content.value}?`))
        {
            api.Rethread(new RethreadParameter(ids, content.value), new ApiHandler(d =>
            {
                alert(`Rethread successful: ${d.result.length} comments`);
            }));
        }
    };
    div.appendChild(content);
    div.appendChild(button);
    return div;
}