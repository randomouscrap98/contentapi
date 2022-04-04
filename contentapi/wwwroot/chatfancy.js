
//NOTE: Nothing in this file is used to make the chat work! Stick to chat.html, that has everything you really need!
//I just wanted to make the chat a little nicer, think of this as a "plugin" if you will
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

    themeselect.oninput = updateTheme;

    if(settings.theme)
    {
        themeselect.value = settings.theme;
        updateTheme();
    }
});

function updateTheme()
{
    document.body.setAttribute('data-theme', themeselect.value);
    SetFancySettingValue("theme", themeselect.value);
}