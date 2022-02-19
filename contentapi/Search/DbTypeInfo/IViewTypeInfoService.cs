namespace contentapi.Search;

public interface IViewTypeInfoService
{
    ViewTypeInfo GetTypeInfo<T>();
    ViewTypeInfo GetTypeInfo(Type t);
}