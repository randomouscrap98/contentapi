using System.Collections;
using System.Data;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using AutoMapper;
using contentapi.data;
using contentapi.Db;
using contentapi.Utilities;
using Newtonsoft.Json.Linq;

namespace contentapi.Search;

//This assumes that WHATEVER is given, that's EXACTLY what's used. No limits, no nothing.
public class QueryBuilder : IQueryBuilder
{
    protected ILogger logger;
    protected IViewTypeInfoService typeService;
    protected ISearchQueryParser parser;
    protected IMapper mapper;
    protected IPermissionService permissionService;

    public const string DescendingAppend = "_desc";
    public const string CountField = "specialCount";
    public const string CountSelect = $"count(*) as {CountField}";
    public const string PreparseLiteralSugar = "{{.+?}}";
    
    protected readonly Dictionary<string, MacroDescription> StandardMacros = new Dictionary<string, MacroDescription>()
    {
        { "keywordlike", new MacroDescription("v", "KeywordLike", new List<RequestType> { RequestType.content }) },
        { "valuelike", new MacroDescription("vv", "ValueLike", new List<RequestType> { RequestType.content, RequestType.message }) }, 
        { "keywordin", new MacroDescription("v", "KeywordIn", new List<RequestType> { RequestType.content }) },
        { "valuein", new MacroDescription("vv", "ValueIn", new List<RequestType> { RequestType.content, RequestType.message }) }, 
        { "valuekeynotin", new MacroDescription("v", "ValueKeyNotIn", new List<RequestType> { RequestType.content, RequestType.message }) }, 
        { "valuekeynotlike", new MacroDescription("v", "ValueKeyNotLike", new List<RequestType> { RequestType.content, RequestType.message }) }, 
        { "onlyparents", new MacroDescription("", "OnlyParents", new List<RequestType> { RequestType.content }) },
        { "onlynotparents", new MacroDescription("", "OnlyNotParents", new List<RequestType> { RequestType.content }) },
        { "userpage", new MacroDescription("v", "OnlyUserpage", new List<RequestType> { RequestType.content }) },
        { "basichistory", new MacroDescription("", "BasicHistory", new List<RequestType> { RequestType.activity }) },
        { "notdeleted", new MacroDescription("", "NotDeletedMacro", new List<RequestType> { RequestType.content, RequestType.message, RequestType.user }) }, 
        { "notnull", new MacroDescription("f", "NotNullMacro", Enum.GetValues<RequestType>().ToList()) },
        { "null", new MacroDescription("f", "NullMacro", Enum.GetValues<RequestType>().ToList()) },
        { "usertype", new MacroDescription("i", "UserTypeMacro", new List<RequestType> { RequestType.user }) },
        { "ingroup", new MacroDescription("v", "InGroupMacro", new List<RequestType> { RequestType.user }) },
        { "activebans", new MacroDescription("", "ActiveBansMacro", new List<RequestType> { RequestType.ban }) },
        //WARN: permission limiting could be very dangerous! Make sure that no matter how the user uses
        //this, they still ONLY get the stuff they're allowed to read!
        { "receiveuserlimit", new MacroDescription("v", "ReceiveUserLimit", new List<RequestType> { RequestType.message }) },
        { "permissionlimit", new MacroDescription("vfi", "PermissionLimit", new List<RequestType> {
            RequestType.activity,
            RequestType.content,
            RequestType.message,
            RequestType.message_aggregate,
            RequestType.activity_aggregate
        }) }
    };

    protected List<Type> ViewTypes;
    protected Dictionary<RequestType, Type> StandardViewRequests;

    public QueryBuilder(ILogger<QueryBuilder> logger, IViewTypeInfoService typeInfoService, 
        IMapper mapper, ISearchQueryParser parser, IPermissionService permissionService)
    {
        this.logger = logger;
        this.typeService = typeInfoService;
        this.mapper = mapper;
        this.parser = parser;
        this.permissionService = permissionService;

        var assembly = System.Reflection.Assembly.GetAssembly(typeof(contentapi.data.Views.UserView)) ?? throw new InvalidOperationException("NO ASSEMBLY FOR QUERYBUILDER???");

        //Pull the view types out, compute the STANDARD mapping of requests to views. Requests that don't have a standard mapping have something custom and don't go through
        //any of the standard codepaths (they do their own thing entirely)
        ViewTypes = assembly.GetTypes().Where(t => String.Equals(t.Namespace, $"{nameof(contentapi)}.{nameof(contentapi.data)}.{nameof(contentapi.data.Views)}", StringComparison.Ordinal)).ToList();
        var typeInfos = ViewTypes.Select(x => typeInfoService.GetTypeInfo(x));
        StandardViewRequests = typeInfos.Where(x => x.requestType.HasValue).ToDictionary(
            k => k.requestType ?? throw new InvalidOperationException("How did the HasValue check fail on StandardViewRequest build??"), v => v.type);
        
        if(StandardViewRequests.Count == 0)
            throw new InvalidOperationException("NO VIEWS FOUND FOR CACHING IN QUERY BUILDER!! Check the namespace!");
    }


    // ------------
    // -- MACROS --
    // ------------

    public string KeywordSearchGeneric(SearchRequestPlus request, string value, string op, string contentop)
    {
        var typeInfo = typeService.GetTypeInfo<ContentKeyword>();
        return $@"id {contentop}
            (select {nameof(ContentKeyword.contentId)} 
             from {typeInfo.selfDbInfo?.modelTable}
             where {nameof(ContentKeyword.value)} {op} {value}
            )";
    }

    public string ValueSearchGeneric(SearchRequestPlus request, string key, string value, string op, string contentop)
    {
        var typeInfo = typeService.GetTypeInfo<ContentValue>();
        return $@"id {contentop}
            (select {nameof(ContentValue.contentId)} 
             from {typeInfo.selfDbInfo?.modelTable}
             where {nameof(ContentValue.key)} {op} {key} 
               and {nameof(ContentValue.value)} {op} {value}
            )";
    }

    public string ValueKeySearchGeneric(SearchRequestPlus request, string key, string op, string contentop)
    {
        var typeInfo = typeService.GetTypeInfo<ContentValue>();
        return $@"id {contentop}
            (select {nameof(ContentValue.contentId)} 
             from {typeInfo.selfDbInfo?.modelTable}
             where {nameof(ContentValue.key)} {op} {key} 
            )";
    }

    //NOTE: Even though these might say "0" references, they're all used by the macro system!
    public string KeywordLike(SearchRequestPlus request, string value) =>
        KeywordSearchGeneric(request, value, "like", "in");
        
    public string KeywordIn(SearchRequestPlus request, string value) =>
        KeywordSearchGeneric(request, value, "in", "in");

    public string ValueLike(SearchRequestPlus request, string key, string value) =>
        ValueSearchGeneric(request, key, value, "like", "in");

    public string ValueIn(SearchRequestPlus request, string key, string value) =>
        ValueSearchGeneric(request, key, value, "in", "in");
    
    public string ValueKeyNotIn(SearchRequestPlus request, string key)
    {
        return ValueKeySearchGeneric(request, key, "IN", "not in");
    }

    public string ValueKeyNotLike(SearchRequestPlus request, string key)
    {
        return ValueKeySearchGeneric(request, key, "LIKE", "not in");
    }

    public string ParentQueryGenericMacro(SearchRequestPlus request, bool parents)
    {
        var typeInfo = typeService.GetTypeInfo<Content>();
        return $@"id {(parents ? "" : "not")} in 
            (select {nameof(Content.parentId)} 
             from {typeInfo.selfDbInfo?.modelTable}
             group by {nameof(Content.parentId)}
            )";
    }

    public string OnlyParents(SearchRequestPlus request) => ParentQueryGenericMacro(request, true);
    public string OnlyNotParents(SearchRequestPlus request) => ParentQueryGenericMacro(request, false);

    public string OnlyUserpage(SearchRequestPlus request, string userIdValue)
    {
        var typeInfo = typeService.GetTypeInfo<Content>();
        return $@"id =
            (select min({nameof(Content.id)})
             from {typeInfo.selfDbInfo?.modelTable}
             where {nameof(Content.contentType)} = {(long)InternalContentType.userpage}
             and {nameof(Content.createUserId)} = {userIdValue}
             and deleted = 0
            )";
    }

    public string BasicHistory(SearchRequestPlus request)
    {
        var typeInfo = typeService.GetTypeInfo<Content>();
        return $@"contentId in 
            (select {nameof(Content.id)} 
             from {typeInfo.selfDbInfo?.modelTable}
             where contentType = {(int)InternalContentType.page}
             and deleted = 0
            )";
    }

    public string InGroupMacro(SearchRequestPlus request, string group)
    {
        var typeInfo = typeService.GetTypeInfo<UserRelation>();
        return $@"id in 
            (select {nameof(Db.UserRelation.userId)} 
             from {typeInfo.selfDbInfo?.modelTable}
             where {nameof(Db.UserRelation.relatedId)} = {group}
            )";
    }

    public string ActiveBansMacro(SearchRequestPlus request)
    {
        var typeInfo = typeService.GetTypeInfo<Ban>();
        var now = DateTime.UtcNow.ToString(Constants.DateFormat);
        //Active bans are such that the expire date is in the future, but bans don't stack!
        //Only the VERY LAST ban is the active one (hence the max). This could be a "none" type,
        //so we filter that out in the outside
        return $@"{nameof(Ban.type)} <> {(int)BanType.none} and id in 
            (select max({nameof(Ban.id)})
             from {typeInfo.selfDbInfo?.modelTable}
             where {nameof(Ban.expireDate)} > '{now}'
             group by {nameof(Ban.bannedUserId)}
            )";
    }


    public string NotNullMacro(SearchRequestPlus request, string field) { return $"{field} IS NOT NULL"; }
    public string NullMacro(SearchRequestPlus request, string field) { return $"{field} IS NULL"; }
    public string UserTypeMacro(SearchRequestPlus request, string type) { return EnumMacroSearch<UserType>(type); }
    public string NotDeletedMacro(SearchRequestPlus request) { return "deleted = 0"; }

    public string ReceiveUserLimit(SearchRequestPlus request, string requester)
    {
        var typeInfo = typeService.GetTypeInfo<Message>();
        //It is OK if requester is 0, because it'st he same check as the previous...
        return $@"receiveUserId = 0 or receiveUserId = {requester}";
    }

    //NOTE: Even though these might say "0" references, they're all used by the macro system!
    //For now, this is JUST read limit!!
    public string PermissionLimit(SearchRequestPlus request, string requesters, string idField, string type)
    {
        var typeInfo = typeService.GetTypeInfo<ContentPermission>();
        var checkCol = permissionService.ActionToColumn(permissionService.StringToAction(type));

        //Note: we're checking createUserId against ALL requester values they gave us! This is OK, because the
        //additional values are things like 0 or their groups, and groups can't create content
        return $@"({idField} in 
            (select {nameof(ContentPermission.contentId)} 
             from {typeInfo.selfDbInfo?.modelTable}
             where {nameof(ContentPermission.userId)} in {requesters}
               and `{checkCol}` = 1
            ))"; //NOTE: DO NOT CHECK CREATE USER! ALL PERMISSIONS ARE NOW IN THE TABLE! NOTHING IMPLIED!
    }

    /// <summary>
    /// A helper to generate optimized clauses against enum fields using a string type
    /// </summary>
    public string EnumMacroSearch<T>(string type, string name = "type") where T : struct, System.Enum 
    {
        T result;
        if(!Enum.TryParse<T>(type, out result))
            throw new ArgumentException($"Unknown type '{type}' for set '{typeof(T).Name}'");
        return $"{name} = {Unsafe.As<T, int>(ref result)}";
    }

    // -------------
    // -- PARSING --
    // -------------

    /// <summary>
    /// Throw exceptions on any funky business we can quickly report
    /// </summary>
    /// <param name="requests"></param>
    public SearchRequestPlus StandardRequestPreparse(SearchRequest request, Dictionary<string, object> parameters)
    {
        var acceptedTypes = Enum.GetNames<RequestType>();

        //Oops, unknown type
        if(!acceptedTypes.Contains(request.type))
            throw new ArgumentException($"Unknown request type: {request.type} in request {request.name}");
        
        //Users HAVE to name their stuff! Otherwise the output of the data
        //dictionary won't make any sense! Note that the method for this check 
        //isn't EXACTLY right but... it should be fine, fields are all
        //just regular identifiers, like in a programming language.
        if(!parser.IsFieldNameValid(request.name))
            throw new ArgumentException($"Malformed name '{request.name}'");

        //Oops, there's a name collision! Might as well fail as early as possible
        if(parameters.ContainsKey(request.name))
            throw new ArgumentException($"Request name {request.name} collides with a key in your value array");
        
        if(string.IsNullOrWhiteSpace(request.fields))
            throw new ArgumentException($"No 'fields' value given in '{request.name}', try setting 'fields' to '*'");

        //Now do some pre-parsing and data retrieval. If one of these fail, it's good that it
        //fails early (although it will only fail if me, the programmer, did something wrong,
        //not if the user supplies a weird request)
        var reqplus = mapper.Map<SearchRequestPlus>(request);
        reqplus.requestType = Enum.Parse<RequestType>(reqplus.type); //I know this will succeed
        reqplus.typeInfo = typeService.GetTypeInfo(StandardViewRequests[reqplus.requestType]);
        reqplus.requestFields = ComputeRealFields(reqplus);

        //A special check: ids are required for non-query-builder fields. Assume ALL ids are named like Content's id
        var nonBuildableFields = reqplus.requestFields.Where(x => reqplus.typeInfo.fields.ContainsKey(x) && !reqplus.typeInfo.fields[x].queryBuildable).ToList();
        if(nonBuildableFields.Count > 0 && !reqplus.requestFields.Contains(nameof(Content.id)))
            throw new ArgumentException($"You MUST select field '{nameof(Content.id)}' when also selecting complex fields such as '{nonBuildableFields.First()}' in request '{request.name}'");

        return reqplus;
    }

    /// <summary>
    /// Return the field selector for the given field. For instance, it might be a 
    /// simple "username", or it might be "(registered IS NULL) AS registered" etc.
    /// </summary>
    /// <remarks> MOST fields should work with this function, either it's the same name,
    /// or it's a simple remap from an attribute, or it's slightly complex but stored
    /// in our dictionary of remaps </remarks>
    /// <param name="fieldName"></param>
    /// <param name="r"></param>
    /// <returns></returns>
    public string StandardFieldSelect(string fieldName, SearchRequestPlus r)
    {
        //Just a simple count bypass; counts are a special thing that can be added to any query, so this is safe.
        if(fieldName == CountField)
            return CountSelect;

        var c = r.typeInfo.fields[fieldName].fieldSelect;

        if(string.IsNullOrWhiteSpace(c))
            throw new InvalidOperationException($"Can't select field '{fieldName}' in base query: no 'select' sql defined for that field!");
        else if (c == fieldName)
            return c;
        else
            return $"({c}) AS {fieldName}";
    }

    /// <summary>
    /// Compute the VIEW fields (named output, not db columns) which were requested in the given SearchRequestPlus
    /// </summary>
    /// <param name="r"></param>
    /// <returns></returns>
    public List<string> ComputeRealFields(SearchRequestPlus r)
    {
        bool inverted = false;

        if(r.fields.StartsWith("~"))
        {
            inverted = true;
            r.fields = r.fields.TrimStart('~');
        }

        var fields = r.fields.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToList(); 

        //Redo fieldlist if they asked for special formats
        if (r.fields == "*")
            fields = new List<string>(r.typeInfo.fields.Keys);
        
        if (inverted)
            fields = r.typeInfo.fields.Keys.Except(fields).ToList();
        
        //Check for bad fields. NOTE: this means we can guarantee that future checks against the typeinfo are safe... or can we?
        //What about parsing fields from the query string?
        foreach (var field in fields)
        {
            if (!r.typeInfo.fields.ContainsKey(field) && field != CountField)
            {
                throw new ArgumentException($"Unknown field {field} in request {r.name}");
            }
        }
        
        return fields;
    }

    public string CombineQueryClause(string baseQuery, string clause)
    {
        if(string.IsNullOrWhiteSpace(baseQuery))
            return clause;
        else if (!string.IsNullOrWhiteSpace(clause))
            return $"({baseQuery}) AND ({clause})";
        return baseQuery; 
    }

    /// <summary>
    /// Returns the field as it should be named inside a query. Most of the time, this is the name from the view and nothing else
    /// </summary>
    /// <param name="field"></param>
    /// <param name="request"></param>
    /// <returns></returns>
    public string ParseField(string field, SearchRequestPlus request, string parseFor = "query")
    {
        //These special 'extraQueryFields' can be used regardless of any rules
        if(request.typeInfo.extraQueryFields.ContainsKey(field))
            return request.typeInfo.extraQueryFields[field];

        if(!request.typeInfo.fields.ContainsKey(field))
            throw new ArgumentException($"Field '{field}' not found in type '{request.type}'({request.name})! (in: {parseFor})");

        if(!request.typeInfo.fields[field].queryable)
            throw new ArgumentException($"Field '{field}' not queryable in type '{request.type}'({request.name})! (in: {parseFor})");

        //For now, we outright reject querying against fields you don't explicitly pull. This CAN be made better in the
        //future, but for now, I think this is a reasonable limitation to reduce potential bugs
        if(!request.requestFields.Contains(field))
            throw new ArgumentException($"Can't query against field '{field}' without selecting it (in: {parseFor}): Current query system requires fields to be selected in order to be used anywhere else");

        return field;
    }

    /// <summary>
    /// Find the ACTUAL object the given "value" is looking for, assuming the given set of keys
    /// is in order and the given startingObject is where to start looking for values. May simply be a single key,
    /// in which case this is just a very slow dictionary lookup
    /// </summary>
    /// <remarks>
    /// This function ISN'T just an object/key traveller, it ALSO can retrieve lists of values, such as if you do
    /// @content.id where content is a list, it will return all the ids inside content.
    /// </remarks>
    /// <param name="valueKey"></param>
    /// <param name="startingObject"></param>
    /// <returns></returns>
    public object FindValueObject(string valueKey, object startingObject)
    {
        var realValName = valueKey.TrimStart('@');

        //First, go down and down through the dot list and ensure each thing is a property of an object or a key
        //in a dictionary
        var dotParts = realValName.Split(".".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

        object result = startingObject;

        //Each dot part needs to be a child of the current container.
        foreach(var part in dotParts)
        {
            var resultType = result.GetType();
            var parentType = resultType;
            IEnumerable<object>? resultStronglyTyped = null;
            IEnumerable<IDictionary<string, object>>? resultStronglyTypedDic = null;

            if(resultType.IsGenericEnumerable())
            {
                var empty = false;

                if(result is IEnumerable<IDictionary<string, object>>)
                {
                    resultStronglyTypedDic = (IEnumerable<IDictionary<string, object>>)result;
                    empty = resultStronglyTypedDic.Count() == 0;
                    parentType = typeof(IDictionary<string, object>);
                }
                else
                {
                    resultStronglyTyped = (IEnumerable<object>)result;
                    empty = resultStronglyTyped.Count() == 0;
                    if(!empty)
                        parentType = resultStronglyTyped.First().GetType();
                }

                if(empty)
                {
                    logger.LogDebug($"No resultset for value '{valueKey}', returning empty list while still on node '{part}'");
                    return new List<object>();
                }

            }

            var singleRetriever = new Func<object, object?>(o => null);

            //It's probably a dictionary... maybe
            if(parentType.IsAssignableTo(typeof(IDictionary<string, object>)))
            {
                singleRetriever = (o) => 
                {
                    var d = (o as IDictionary<string, object>) ?? throw new InvalidOperationException($"Couldn't cast value part '{part}' in '{valueKey}' to IDictionary<string, object>!");
                    return d.ContainsKey(part) ? d[part] : null;
                };
            }
            else if(parentType.IsGenericDictionary())
            {
                singleRetriever = (o) => 
                {
                    var d = (o as IDictionary) ?? throw new InvalidOperationException($"Couldn't cast value part '{part}' in '{valueKey}' to IDictionary!");
                    return d.Contains(part) ? d[part] : null;
                };
            }
            //Otherwise, assume it is an object
            else
            {
                var properties = parentType.GetProperties();
                var partProperty = properties.FirstOrDefault(x => x.Name == part);

                if(partProperty == null)
                    throw new ArgumentException($"Couldn't find '{part}' in value '{valueKey}'");

                singleRetriever = (o) => partProperty.GetValue(o) ?? throw new InvalidOperationException(
                    $"Even after checking if property exists, object didn't have key '{part} in value '{valueKey}'"
                );
            }

            //These two different assignments are for the list feature: either the value selector is applied across a whole list,
            //or it's just the normal way
            if(resultStronglyTyped != null)
                result = resultStronglyTyped.Select(x => singleRetriever(x)).Where(x => x != null);
            else if(resultStronglyTypedDic != null)
                result = resultStronglyTypedDic.Select(x => singleRetriever(x)).Where(x => x != null);
            else
                result = singleRetriever(result) ??  throw new InvalidOperationException($"Couldn't find node '{part}' in '{valueKey}'");
        }

        //At the end of the loop, the result should contain the complex 
        return FlattenResult(result);
    }

    /// <summary>
    /// Tries to flatten two-layer lists. If you pass a scalar, nothing is modified. If you pass a single-level list, nothing is 
    /// modified. But, if you pass a nested list, it will flatten it to a single layer list
    /// </summary>
    /// <param name="result"></param>
    /// <returns></returns>
    public object FlattenResult(object result)
    {
        var endResultType = result.GetType();

        //Not necessarily complicated if it's an enumerable (it usually is)
        if(endResultType.IsGenericEnumerable())
        {
            var resultList = (IEnumerable<object>)result;

            if(resultList.Count() == 0)
                return new List<object>();

            var resultInnerType = resultList.First().GetType();

            //Only if the list is a list of lists does it get super complicated
            if(resultInnerType.IsGenericEnumerable())
                return PrepareResultTyping(resultList.SelectMany(x => ((IEnumerable)x).Cast<object>()));
            else
                return PrepareResultTyping(resultList);
        }

        return result;
    }

    public List<object?> PrepareResultTyping(IEnumerable<object> results)
    {
        var flatResult = new List<object?>();

        foreach (var r in results) 
        {
            var rtype = r.GetType();

            if (rtype == typeof(JValue))
            {
                var jval = (JValue)r;
                flatResult.Add(jval.Value);
            }
            else if (rtype == typeof(JArray))
            {
                flatResult.AddRange(PrepareResultTyping((JArray)r));
            }
            else
            {
                flatResult.Add(r);
            }
        }

        return flatResult;
    }

    public string ParseValue(string value, SearchRequestPlus request, Dictionary<string, object> parameters)
    {
        var realValName = value.TrimStart('@');
        var newName = value.Replace("@", "").Replace(".", "_");

        if (!parameters.ContainsKey(newName))
        {
            var valueObject = FindValueObject(value, parameters);
            parameters.Add(newName, valueObject);
        }

        return $"@{newName}";
    }
    
    public string ParseMacro(string m, string a, SearchRequestPlus request, Dictionary<string, object> parameters)
    {
        if(!StandardMacros.ContainsKey(m))
            throw new ArgumentException($"Macro {m} not found for request '{request.name}'");
        
        var macDef = StandardMacros[m];
        var args = a.Split(",", StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToList();

        if(args.Count != macDef.argumentTypes.Count)
            throw new ArgumentException($"Expected {macDef.argumentTypes.Count} arguments for macro {m} in '{request.name}', found {args.Count}");
        
        //Parameters start with the request, always
        var argVals = new List<object?>() {
            request
        };

        for(var i = 0; i < args.Count; i++)
        {
            var expArgType = macDef.argumentTypes[i];
            var ex = new ArgumentException($"Argument #{i + 1} in macro {m} for request '{request.name}': expected {macDef.argumentTypes[i]} type");
            if(expArgType == MacroArgumentType.value)
            {
                if(!args[i].StartsWith("@"))
                    throw ex;
                argVals.Add(ParseValue(args[i], request, parameters));
            }
            else if(expArgType == MacroArgumentType.field)
            {
                if(args[i].StartsWith("@"))
                    throw ex;
                argVals.Add(ParseField(args[i], request));
            }
            else if(expArgType == MacroArgumentType.fieldImmediate)
            {
                if(args[i].StartsWith("@"))
                    throw ex;
                argVals.Add(args[i]); //Use the IMMEDIATE value!
            }
            else
            {
                throw new InvalidOperationException($"Unknown macro argument type {macDef.argumentTypes[i]} in request {request.name}");
            }
        }

        //At this point, we have the macro function info, so we can just call it
        return (string)(macDef.macroMethod.Invoke(this, argVals.ToArray()) ?? 
            throw new InvalidOperationException($"Macro method for macro {m} returned null in request {request.name}!"));
    }

    /// <summary>
    /// This method performs a "standard" user-query parse and adds the generated sql to the 
    /// given queryStr, along with any missing parameters that were able to be computed.
    /// </summary>
    /// <param name="queryStr"></param>
    /// <param name="request"></param>
    /// <param name="parameters"></param>
    public string CreateStandardQuery(StringBuilder queryStr, SearchRequestPlus request, Dictionary<string, object> parameters)
    {
        //Not sure if the "query" is generic enough to be placed outside of standard... mmm maybe
        try
        {
            var parseResult = parser.ParseQuery(request.query, 
                f => ParseField(f, request),
                v => ParseValue(v, request, parameters), 
                (m, a) => ParseMacro(m, a, request, parameters)
            );

            return parseResult;

        }
        //We know how to handle these exceptions
        catch (ParseException) { throw; }
        catch (ArgumentException) { throw; }
        catch (Exception ex)
        {
            //Convert to argument exception so the user knows what's up. Nothing that happens here
            //is due to a database or other "internal" server error (other than stupid messups on my part)
            logger.LogWarning($"Unknown exception during query parse: {ex}");
            throw new ParseException($"Parse error: {ex.Message}");
        }
    }

    /// <summary>
    /// This method adds a 'standard' select for regular searches against simple
    /// single table queries. For instance, it might be "SELECT id,username FROM users "
    /// </summary>
    /// <param name="queryStr"></param>
    /// <param name="r"></param>
    public void AddStandardSelect(StringBuilder queryStr, SearchRequestPlus r)
    {
        //This enforces the "count is the only field" thing
        var fieldSelect = 
            r.requestFields.Where(x => x == CountField || r.typeInfo.fields[x].queryBuildable).Select(x => StandardFieldSelect(x, r)).ToList();
       // r.requestFields.Contains(CountField) ? 
        //    new List<string> { CountSelect } :

        var selectFrom = r.typeInfo.selectFromSql ; 

        if(string.IsNullOrWhiteSpace(selectFrom))
            throw new InvalidOperationException($"Standard select {r.type} doesn't define a 'select from' statement in request {r.name}, this is a program error!");

        queryStr.Append("SELECT ");
        queryStr.Append(string.Join(",", fieldSelect));
        queryStr.Append(" FROM ");
        queryStr.Append(selectFrom);
        queryStr.Append(" "); //To be nice, always end in space?
    }


    /// <summary>
    /// Assuming a query that can be limited simply, this adds the necessary LIMIT, OFFSET,
    /// and ORDER BY clauses. Most queries can use this function.
    /// </summary>
    /// <param name="queryStr"></param>
    /// <param name="r"></param>
    /// <param name="parameters"></param>
    public void AddStandardFinalLimit(StringBuilder queryStr, SearchRequestPlus r, Dictionary<string, object> parameters)
    {
        if(!string.IsNullOrEmpty(r.order))
        {
            var orders = r.order.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

            //Can't parameterize the column... inserting directly; scary. Also, immediately adding the order by even
            //though we don't know what we're doing yet... eehhh should be fine?
            queryStr.Append("ORDER BY ");

            for(int i = 0; i < orders.Length; i++)
            {
                var order = orders[i];
                var descending = false;

                if (order.EndsWith(DescendingAppend))
                {
                    descending = true;
                    order = order.Substring(0, order.Length - DescendingAppend.Length);
                }

                //We don't need the result, just the checking
                var parsedOrder = ParseField(order, r);

                queryStr.Append(order);

                if(r.typeInfo.fields[order].fieldType == typeof(string))
                    queryStr.Append(" COLLATE NOCASE ");

                if (descending)
                    queryStr.Append(" DESC ");
                
                if(i < orders.Length - 1)
                    queryStr.Append(", ");
                else
                    queryStr.Append(" ");
            }
        }

        //ALWAYS need limit if you're doing offset, just easier to include it. -1 is magic
        var limitKey = r.UniqueRequestKey("limit"); 
        queryStr.Append($"LIMIT @{limitKey} ");
        parameters.Add(limitKey, r.limit); //WARN: this modifies the parameters!

        if (r.skip > 0)
        {
            var skipKey = r.UniqueRequestKey("skip");
            queryStr.Append($"OFFSET @{skipKey} ");
            parameters.Add(skipKey, r.skip);
        }
    }

    public SearchRequestPlus FullParseRequest(SearchRequest request, Dictionary<string, object> parameters)
    {
        var queryStr = new StringBuilder();

        //WARN: doing a standard preparse before knowing it's a standard request! fix some of this
        var reqplus = StandardRequestPreparse(request, parameters);

        //Find all the shortcut preparser syntax sugar and convert to values
        var literals = Regex.Match(reqplus.query, PreparseLiteralSugar);
        var count = 1;

        while(literals.Success)
        {
            var valueName = reqplus.UniqueRequestKey($"val{count}");
            parameters.Add(valueName, literals.Value.Substring(2, literals.Value.Length - 4));
            reqplus.query = reqplus.query.Replace(literals.Value, $"@{valueName}");
            count++;
            literals = Regex.Match(reqplus.query, PreparseLiteralSugar);
        }

        if(StandardViewRequests.ContainsKey(reqplus.requestType))
        {
            //Generate "select from"
            AddStandardSelect(queryStr, reqplus);

            //Generate "where (x)"
            var whereQuery = CreateStandardQuery(queryStr, reqplus, parameters);    
            whereQuery = CombineQueryClause(whereQuery, reqplus.typeInfo.whereSql);

            if (!string.IsNullOrWhiteSpace(whereQuery))
                queryStr.Append($"WHERE {whereQuery} ");
            
            if(!string.IsNullOrWhiteSpace(reqplus.typeInfo.groupBySql))
                queryStr.Append($"GROUP BY {reqplus.typeInfo.groupBySql} ");

            //Generate "order by limit offset"
            AddStandardFinalLimit(queryStr, reqplus, parameters);
        }
        else
        {
            throw new NotImplementedException($"Sorry, {request.type} isn't ready yet!");
        }

        reqplus.computedSql = queryStr.ToString();
        return reqplus;
    }

    public AboutSearch GetAboutSearch()
    {
        var result = new AboutSearch();

        //The types are from our enum are the types that can be searched
        foreach(var type in Enum.GetValues<RequestType>())
        {
            if(StandardViewRequests.ContainsKey(type))
            {
                var typeInfo = typeService.GetTypeInfo(StandardViewRequests[type]);
                result.types.Add(type.ToString(), typeInfo.fields.ToDictionary(x => x.Key, y => mapper.Map<AboutSearchField>(y.Value)));

                result.objects.Add(type.ToString(), Activator.CreateInstance(StandardViewRequests[type]) ??
                    throw new InvalidOperationException($"Couldn't create type {type} for display!"));
            }
        }

        foreach(var macro in StandardMacros.Keys)
        {
            var macdef = StandardMacros[macro];
            result.macros.Add(macro, new {
                format = $"!{macro}({string.Join(",", macdef.argumentTypes.Select(x => x.ToString()))})",
                allowedtypes = macdef.allowedTypes.Select(x => x.ToString())
            });
        }

        result.codes.Add(typeof(UserAction).Name, Enum.GetValues<UserAction>().ToDictionary(x => (int)x, y => y.ToString("G")));
        result.codes.Add(typeof(InternalContentType).Name, Enum.GetValues<InternalContentType>().ToDictionary(x => (int)x, y => y.ToString("G")));
        result.codes.Add(typeof(UserType).Name, Enum.GetValues<UserType>().ToDictionary(x => (int)x, y => y.ToString("G")));
        result.codes.Add(typeof(BanType).Name, Enum.GetValues<BanType>().ToDictionary(x => (int)x, y => y.ToString("G")));
        result.codes.Add(typeof(AdminLogType).Name, Enum.GetValues<AdminLogType>().ToDictionary(x => (int)x, y => y.ToString("G")));
        result.codes.Add(typeof(VoteType).Name, Enum.GetValues<VoteType>().ToDictionary(x => (int)x, y => y.ToString("G")));
        result.codes.Add(typeof(EventType).Name, Enum.GetValues<EventType>().ToDictionary(x => (int)x, y => y.ToString("G")));

        return result;
    }
}