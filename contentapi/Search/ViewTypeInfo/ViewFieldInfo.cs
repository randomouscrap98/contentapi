using System.Reflection;

namespace contentapi.Search;

/// <summary>
/// Our INTERNAL data object that describes a field for a db (or db view) type. NOT what the users get when they ask about the api!
/// </summary>
public class ViewFieldInfo
{
    /// <summary>
    /// The literal, unchanged property info given by reflection for the field (property)
    /// </summary>
    /// <value></value>
    public PropertyInfo? rawProperty {get;set;}

    /// <summary>
    /// These are fields which can be used in the "query" part of a request, ie queried against
    /// </summary>
    public bool queryable {get;set;} = false;

    /// <summary>
    /// The SQL for how to actually query this particular field. If it's null, it's not queryable within
    /// the standard query builder
    /// </summary>
    public string? fieldSelect {get;set;}
    
    public bool multiline {get;set;} = false;

    /// <summary>
    /// An estimated rating for how expensive this field is to pull. For additional clarity, fields which can't be pulled will have this set to -1
    /// </summary>
    public int expensive {get;set;} = -1;

    /// <summary>
    /// Whether this particular field is even pulled from the database or done within the api itself. It is RARE for
    /// a field to be computed...
    /// </summary>
    //public bool computed {get;set;} = false;


    public WriteRule? onInsert {get;set;}
    public WriteRule? onUpdate {get;set;}

    /// <summary>
    /// If this is null or empty, there is "no" backing database conversion at all! For safety, we define complex fields with a dbcolumn of ""
    /// </summary>
    /// <value></value>
    //public string? realDbColumn {get;set;} = "";


    /// <summary>
    /// A shortcut to the type info, stored inside rawProperty
    /// </summary>
    public Type fieldType => rawProperty?.PropertyType ?? throw new InvalidOperationException("No type for db field somehow??");

    /// <summary>
    /// Whether this field can be included in the basic query builder, used to pull almost all data
    /// </summary>
    public bool queryBuildable => !string.IsNullOrWhiteSpace(fieldSelect);

}
