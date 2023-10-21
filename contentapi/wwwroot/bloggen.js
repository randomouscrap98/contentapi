function page_onparse()
{
    var rawText = content_raw.textContent;
    var markupLang = 
        content_rendered.getAttribute("data-markup") ||
        content_rendered.getAttribute("data-markuplang") ||
        "plaintext";

    var rendered = Markup.convert_lang(rawText, markupLang, content_rendered);

    content_rendered.style = "";
    content_raw.style.display = "none";

    var times = [...document.querySelectorAll("time")];
    times.forEach(x =>
    {
        var date = new Date(x.getAttribute("datetime"));

        if(x.hasAttribute("data-shortdate"))
            x.textContent = date.toLocaleDateString();
        else
            x.textContent = date.toLocaleString();
    });
}

if (document.readyState == 'loading')
	document.addEventListener('DOMContentLoaded', page_onparse)
else
	page_onparse()