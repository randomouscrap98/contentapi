namespace contentapi.Search;

public interface IDbTypeInfoService
{
    DbTypeInfo GetTypeInfo<T>();
    DbTypeInfo GetTypeInfo(Type t);
}