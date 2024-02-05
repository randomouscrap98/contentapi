
// Required for 12y parser to work
var Nav = {
    replacements: {
        "/": "-", //Replace the first / with - (will deeper paths work...?)
        "?": "&", //Replace the first ? with &
        "pages": "page", //Want some plurals to go to non-plural
        "users": "user",
        "categories": "category",
        "category" : "page"
    },
    link: function (path, element) {
        var a = document.createElement("a");
        a.textContent = path;
        var p = path.replace(/^sbs:/i, "");
        Object.keys(Nav.replacements).forEach(x => p = p.replace(x, Nav.replacements[x]));

        if (p.startsWith("page")) {
            a.href = "index.html?t=" + p.replace("-", "&pid=");
        }
        else {
            a.href = "#";
            a.onclick = (e) => {
                e.preventDefault();
                alert("This frontend doesn't understand this link!");
            };
        }
        return a;
    }
};

HTMLImageElement.prototype.decode = function() {
    return Promise.resolve(true);
};