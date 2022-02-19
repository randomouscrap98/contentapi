using System.Collections.ObjectModel;
using System.Reflection;
using AutoMapper;

namespace contentapi.Search;

public class AboutSearchField
{
    /// <summary>
    /// These are fields which can be used in the "query" part of a request, ie queried against
    /// </summary>
    /// <value></value>
    public bool queryable {get;set;} = false;

    /// <summary>
    /// Whether this particular field is even pulled from the database or done within the api itself. It is RARE for
    /// a field to be computed...
    /// </summary>
    //public bool computed {get;set;} = false;

    /// <summary>
    /// An estimated rating for how expensive this field is to pull. For additional clarity, fields which can't be pulled will have this set to -1
    /// </summary>
    /// <value></value>
    public int expensive {get;set;} = -1;

    /// <summary>
    /// Whether the field can be written on insert
    /// </summary>
    /// <value></value>
    public bool writableOnInsert {get;set;} = true;

    /// <summary>
    /// Whether the field can be written on update
    /// </summary>
    /// <value></value>
    public bool writableOnUpdate {get;set;} = false;

    public bool multiline {get;set;} = false;

    public bool nullable {get;set;} = false;

    public string type {get;set;} = "unknown";
}

public class AboutSearchFieldProfile : Profile
{
    //This should not be here! Or at least unit test this directly!
    public string TypeToAboutType(Type t)
    {
        if(t == typeof(int) || t == typeof(long) || t == typeof(int?) || t == typeof(long?))
            return "int";
        if(t == typeof(string))
            return "string";
        if(t == typeof(DateTime) || t == typeof(DateTime?))
            return "datetime";
        if(t.IsGenericType)
        {
            var genargs = t.GetGenericArguments();
            if(t.GetGenericTypeDefinition() == typeof(List<>))
                return $"list[{TypeToAboutType(genargs[0])}]";
            if(t.GetGenericTypeDefinition() == typeof(Dictionary<,>))
                return $"keyvalue[{TypeToAboutType(genargs[0])},{TypeToAboutType(genargs[1])}]";
        } 
        if(t == typeof(bool))
            return "bool";
        if(t.IsEnum)
            return $"string({string.Join("|", Enum.GetNames(t))})";

        return "unknown";
    }

    //All taken from https://stackoverflow.com/a/58454489/1066474
    public static bool IsNullable(PropertyInfo property) =>
        IsNullableHelper(property.PropertyType, property.DeclaringType, property.CustomAttributes);

    public static bool IsNullable(FieldInfo field) =>
        IsNullableHelper(field.FieldType, field.DeclaringType, field.CustomAttributes);

    public static bool IsNullable(ParameterInfo parameter) =>
        IsNullableHelper(parameter.ParameterType, parameter.Member, parameter.CustomAttributes);

    private static bool IsNullableHelper(Type memberType, MemberInfo? declaringType, IEnumerable<CustomAttributeData> customAttributes)
    {
        if (memberType.IsValueType)
            return Nullable.GetUnderlyingType(memberType) != null;

        var nullable = customAttributes
            .FirstOrDefault(x => x.AttributeType.FullName == "System.Runtime.CompilerServices.NullableAttribute");
        if (nullable != null && nullable.ConstructorArguments.Count == 1)
        {
            var attributeArgument = nullable.ConstructorArguments[0];
            if (attributeArgument.ArgumentType == typeof(byte[]))
            {
                var args = (ReadOnlyCollection<CustomAttributeTypedArgument>)attributeArgument.Value!;
                if (args.Count > 0 && args[0].ArgumentType == typeof(byte))
                {
                    return (byte)args[0].Value! == 2;
                }
            }
            else if (attributeArgument.ArgumentType == typeof(byte))
            {
                return (byte)attributeArgument.Value! == 2;
            }
        }

        for (var type = declaringType; type != null; type = type.DeclaringType)
        {
            var context = type.CustomAttributes
                .FirstOrDefault(x => x.AttributeType.FullName == "System.Runtime.CompilerServices.NullableContextAttribute");
            if (context != null &&
                context.ConstructorArguments.Count == 1 &&
                context.ConstructorArguments[0].ArgumentType == typeof(byte))
            {
                return (byte)context.ConstructorArguments[0].Value! == 2;
            }
        }

        // Couldn't find a suitable attribute
        return false;
    }

    public AboutSearchFieldProfile()
    {
        this.CreateMap<ViewFieldInfo, AboutSearchField>()
            .ForMember(dest => dest.type, opt => opt.MapFrom(src => TypeToAboutType(src.fieldType)))
            .ForMember(dest => dest.writableOnInsert, opt => opt.MapFrom(src => src.onInsert == WriteRule.User))
            .ForMember(dest => dest.writableOnUpdate, opt => opt.MapFrom(src => src.onUpdate == WriteRule.User))
            .ForMember(dest => dest.nullable, opt => opt.MapFrom(src => IsNullable(src.rawProperty!)));
    }
}