namespace contentapi;

public interface ITypeInfoService
{
    TypeInfo GetTypeInfo<T>();
}