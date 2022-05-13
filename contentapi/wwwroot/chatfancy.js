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
    var settings = GetFancySettings();

    setupThemeSelector(settings);

    var messageZoom = createMessageZoom(settings);
    chatcontrols.insertBefore(messageZoom, autoscrollcontainer);

    var selectShow = createMessageSelectShow();
    chatcontrols.insertBefore(selectShow, autoscrollcontainer);

    var fancySelector = createFancySelector(settings);
    chatcontrols.insertBefore(fancySelector, autoscrollcontainer);

    setupSidebarToggle(settings);

    if(settings.fancy)
    {
        document.body.setAttribute("data-fancy", "true");
        AVATARSIZE = 100;
        window.createUserlistUser = fancyCreateUserlistUser;
    }

    var messagearea = document.getElementById("messagearea");
    var autoscroll = document.getElementById("autoscroll");

    var doAutoScroll = () => { if(autoscroll.checked) messagearea.scrollTop = messagearea.scrollHeight; };

    var oldCreateMessage = window.createMessage;
    window.createMessage = (m) =>
    {
        var container = oldCreateMessage(m);
        var userArea = container.querySelector(".userinfo");
        var deleteButton = container.querySelector(".delete");
        var content = container.querySelector(".content");

        var images = content.querySelectorAll("img");
        [...images].forEach(x => x.onload = doAutoScroll);

        //Split the message into left and right to make more space for the avatar... if it's fancy
        if(settings.fancy)
        {
            var leftCon = this.document.createElement("div");
            var rightCon = this.document.createElement("div");
            leftCon.className = "leftmessage";
            rightCon.className = "rightmessage";
            leftCon.appendChild(container.querySelector(".avatar"));
            rightCon.appendChild(userArea);
            rightCon.appendChild(content);
            container.appendChild(leftCon);
            container.appendChild(rightCon);
        }

        if(!m.module)
        {
            var checkBox = document.createElement("input");
            checkBox.setAttribute("type", "checkbox");
            checkBox.className = "messageselect";
            userArea.insertBefore(checkBox, deleteButton);
        }

        return container;
    }

    var oldUpdateNotification = updateNotification;
    window.updateNotification = (a,b,c,d) =>
    {
        oldUpdateNotification(a, b, c, d);
        updateSideNotify();
    };

    var oldResetNotificationBubble = window.resetNotificationBubble;
    window.resetNotificationBubble = (b) =>
    {
        oldResetNotificationBubble(b);
        updateSideNotify();
    };

    //Should override the boring title for something fancier!
    window.setTitle = fancySetTitle;
});

function fancySetTitle(title, content)
{
    if (content.contentType == 3) {
        var img = document.createElement('img');
        img.src = api.GetFileUrl(content.hash, new FileModifyParameter(AVATARSIZE, true));
        title.appendChild(img);
        var span = document.createElement("span");
        span.textContent = `${content.name}`;
        title.appendChild(span);
    }
    else {
        title.textContent = content.name;
    }

    if (api.IsPrivate(content)) {
        var lock = document.createElement("div");
        lock.textContent = "🔒";
        lock.className = "private";
        title.appendChild(lock);
        title.className = (title.className || "") + " privateparent";
    }
}

function fancyCreateUserlistUser(user, status)
{
    var element = document.createElement("div");
    var img = document.createElement("img");
    img.src = api.GetFileUrl(user.avatar, new FileModifyParameter(AVATARSIZE, true));
    img.className = "avatar";
    element.appendChild(img);
    element.title = user.username;
    element.className = "user";
    return element;
}

function updateSideNotify()
{
    var sidenotify = document.getElementById("sidenotify");
    var alerts = document.querySelectorAll("#activitylist .notification_alert");
    sidenotify.style.visibility = alerts.length ? "visible" : "hidden";
}

function setupSidebarToggle(settings)
{
    var titleicons = document.getElementById("titleicons");

    var sidenotify = document.createElement("div");
    sidenotify.id = "sidenotify";
    sidenotify.textContent = "!";

    var toggle = document.createElement("input");
    toggle.setAttribute("type", "checkbox");
    toggle.id = "sidebartoggle";
    toggle.checked = !settings.sidebarhidden;

    var refreshSidebarToggle = () =>
    {
        SetFancySettingValue("sidebarhidden", !toggle.checked);

        if(toggle.checked)
            document.body.removeAttribute("data-sidebarhidden");
        else
            document.body.setAttribute("data-sidebarhidden", "true");
    };

    toggle.oninput = refreshSidebarToggle;

    titleicons.appendChild(toggle);
    titleicons.appendChild(sidenotify);

    sidenotify.style.visibility = "hidden";

    refreshSidebarToggle();
}

function setupThemeSelector(settings)
{
    var themeselectcontainer = createThemeSelector();
    chatcontrols.insertBefore(themeselectcontainer, autoscrollcontainer);
    var themeselect = document.getElementById("themeselect");

    themeselect.oninput = () => updateTheme(themeselect);

    if(settings.theme)
    {
        themeselect.value = settings.theme;
        updateTheme(themeselect);
    }
}



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

function createFancySelector(settings)
{
    var checkbox = document.createElement("input");
    checkbox.setAttribute("type", "checkbox");
    checkbox.checked = settings.fancy;
    checkbox.oninput = () =>
    {
        SetFancySettingValue("fancy", checkbox.checked);
        location.reload();
    };
    var div = createLabelGeneric("Fancy mode!", checkbox);
    return div;
}

function createMessageZoom(settings)
{
    var checkbox = document.createElement("input");
    var label = null;
    checkbox.setAttribute("type", "range");
    checkbox.setAttribute("step", "0.1");
    checkbox.setAttribute("min", "0.5");
    checkbox.setAttribute("max", "3");
    var refreshZoom = () =>
    {
        SetFancySettingValue("messagezoom", checkbox.value);
        var msglist = document.getElementById("messagelist");
        console.log("Setting zoom to " + checkbox.value);
        msglist.style.fontSize = `${checkbox.value}em`;
        if(label) label.title = checkbox.value;
    };
    checkbox.oninput = refreshZoom;
    checkbox.value = settings.messagezoom || 1;
    var div = createLabelGeneric("MZ", checkbox, true);
    label = div.querySelector("span");
    label.style.verticalAlign = "top";
    refreshZoom();
    return div;
}


function createRethreader()
{
    var div = document.createElement("span")
    var content = document.createElement("input");
    content.setAttribute('placeholder', "pid");
    content.id = "rethreadpid";
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