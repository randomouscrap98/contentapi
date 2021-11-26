using contentapi.Views;

namespace contentapi.Search;

public enum RequestType
{
    [ViewMap(typeof(UserView))] user,
    [ViewMap(typeof(ContentView))] content,
    [ViewMap(typeof(CommentView))] comment,
    [ViewMap(typeof(PageView))] page,
    [ViewMap(typeof(FileView))] file,
    [ViewMap(typeof(ModuleView))] module,
}