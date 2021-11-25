namespace contentapi.Utilities;

public interface ITypeInfoService
{
    TypeInfo GetTypeInfo<T>();
    TypeInfo GetTypeInfo(Type t);
}