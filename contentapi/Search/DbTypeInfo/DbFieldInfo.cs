using System.Reflection;

namespace contentapi.Search;

/// <summary>
/// Our INTERNAL data object that describes a field for a db (or db view) type. NOT what the users get when they ask about the api!
/// </summary>
public class DbFieldInfo
{
    /// <summary>
    /// The literal, unchanged property info given by reflection for the field (property)
    /// </summary>
    /// <value></value>
    public PropertyInfo? rawProperty {get;set;}

    /// <summary>
    /// If this field matches directly with a db model property (which directly represents the database table), THAT field's
    /// property info is stored here.
    /// </summary>
    /// <value></value>
    //public PropertyInfo? matchedModelProperty {get;set;}

    /// <summary>
    /// These are fields which can be used in the "query" part of a request, ie queried against
    /// </summary>
    /// <value></value>
    public bool queryable {get;set;} = false;

    /// <summary>
    /// Whether this particular field is even pulled from the database or done within the api itself. It is RARE for
    /// a field to be computed...
    /// </summary>
    public bool computed {get;set;} = false;

    public bool autoDateOnInsert {get;set;} = false;
    public bool autoDateOnUpdate {get;set;} = false;
    public bool autoUserOnInsert {get;set;} = false;
    public bool autoUserOnUpdate {get;set;} = false;

    public bool preserveOnUpdate {get;set;} = false;

    ///// <summary>
    ///// Whether the field can be written on insert
    ///// </summary>
    ///// <value></value>
    //public bool writableOnInsert {get;set;} = true;

    ///// <summary>
    ///// Whether the field can be written on update
    ///// </summary>
    ///// <value></value>
    //public bool writableOnUpdate {get;set;} = false;

    /// <summary>
    /// If this is null or empty, there is "no" backing database conversion at all! For safety, we define complex fields with a dbcolumn of ""
    /// </summary>
    /// <value></value>
    public string? realDbColumn {get;set;} = "";

    /// <summary>
    /// An estimated rating for how expensive this field is to pull. For additional clarity, fields which can't be pulled will have this set to -1
    /// </summary>
    /// <value></value>
    public int expensive {get;set;} = -1;


    /// <summary>
    /// A shortcut to the type info, stored inside rawProperty
    /// </summary>
    /// <returns></returns>
    public Type fieldType => rawProperty?.PropertyType ?? throw new InvalidOperationException("No type for db field somehow??");

    /// <summary>
    /// A shortcut to the type info of the matching db model field, stored inside matchedModelProperty
    /// </summary>
    //public Type? matchedModelType => matchedModelProperty?.PropertyType;
}
