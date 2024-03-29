using System.Reflection;
using contentapi.data;

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

    /// <summary>
    /// The name used in the 'where' clause. This usually defaults to the 'fieldSelect' value, regardless of what it is. We have this to 
    /// remove ambiguity in select/where with complex selects
    /// </summary>
    /// <value></value>
    public string? fieldWhere {get;set;}

    /// <summary>
    /// The legitimate name of the column as seen in the database. Defaults to 'fieldSelect' value, regardless of what it is. If your
    /// field is not writable, it doesn't matter what this value is
    /// </summary>
    /// <value></value>
    public string? fieldColumn {get;set;}

    /// <summary>
    /// With as much accuracy as possible, what is the ACTUAL database column we're pointing at?
    /// </summary>
    /// <value></value>
    //public string? dbColumn {get;set;}
    
    public bool multiline {get;set;} = false;

    /// <summary>
    /// An estimated rating for how expensive this field is to pull. For additional clarity, fields which can't be pulled will have this set to -1
    /// </summary>
    public int expensive {get;set;} = -1;

    public WriteRule onInsert {get;set;}
    public WriteRule onUpdate {get;set;}

    /// <summary>
    /// A shortcut to the type info, stored inside rawProperty
    /// </summary>
    public Type fieldType => rawProperty?.PropertyType ?? throw new InvalidOperationException("No type for db field somehow??");

    /// <summary>
    /// Whether this field can be included in the basic query builder, used to pull almost all data
    /// </summary>
    public bool queryBuildable => !string.IsNullOrWhiteSpace(fieldSelect);

}
