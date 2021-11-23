namespace contentapi;

public interface ITypeInfoService
{
    TypeInfo GetTypeInfo<T>();
    TypeInfo GetTypeInfo(Type t);
}