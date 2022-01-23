// This is stuff which I might add later, it's fanciness that distracts
// from the core of just using the API

/* 
<!-- templates that you'd need or whatever -->
            <div id="errors" data-errors class="errors">
            </div>
            <div id="error" class="error"></div>
*/

// For simplicity, we have a standard way of adding errors. It's just a simple 
// list of elements within some container, usually appended to the TOP
function AddError(message, container)
{
    var errorContainer = container.querySelector("[data-errors]");
    if(!errorContainer)
    {
        errorContainer = LoadTemplate("errors");
        container.insertBefore(errorContainer, container.firstChild);
    }
    var errorElement = LoadTemplate("error");
    errorElement.textContent = message;
    errorContainer.appendChild(errorElement);
}

// An error handler generator which dumps errors into the given element
function GetErrorDumpHandler(element)
{
    return e => {
        console.error("Api error:", e);
        AddError(`${e.status_code}: ${e.message}`, element);
    };
}
