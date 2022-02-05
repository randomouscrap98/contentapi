using System.Data;
using System.Runtime.CompilerServices;
using System.Text;
using AutoMapper;
using contentapi.Db;
using contentapi.Utilities;
using contentapi.Views;

namespace contentapi.Search;

//This assumes that WHATEVER is given, that's EXACTLY what's used. No limits, no nothing.
public class QueryBuilder : IQueryBuilder
{
    protected ILogger logger;
    protected IDbTypeInfoService typeService;
    protected ISearchQueryParser parser;
    protected IMapper mapper;
    protected IPermissionService permissionService;

    public const string MainAlias = "main";
    public const string DescendingAppend = "_desc";
    public const string NaturalCommentQuery = "deleted = 0 and module IS NULL";
    
    protected static readonly List<RequestType> CONTENTREQUESTTYPES = new List<RequestType>()
    {
        RequestType.content, RequestType.file, RequestType.page, RequestType.module
    };

    public List<RequestType> ContentRequestTypes => CONTENTREQUESTTYPES;

    //These fields are too difficult to modify with the attributes, so we do it in code here
    protected readonly Dictionary<(RequestType, string),string> StandardComplexFields = new Dictionary<(RequestType, string), string> {
        { (RequestType.user, "registered"), $"(registrationKey IS NULL) as registered" },
        //{ (RequestType.user, "deleted"), $"(salt = '') as deleted" }, // We USED to do it like this!
    };

    //Some searches can easily be modified afterwards with constants, put that here.
    protected readonly Dictionary<RequestType, string> StandardSearchModifiers = new Dictionary<RequestType, string>()
    {
       { RequestType.file, $"internalType = {(int)InternalContentType.file}" },
       { RequestType.module, $"internalType = {(int)InternalContentType.module}" },
       { RequestType.page, $"internalType = {(int)InternalContentType.page}" },
       { RequestType.comment, $"module IS NULL" },
    };

    protected readonly Dictionary<string, MacroDescription> StandardMacros = new Dictionary<string, MacroDescription>()
    {
        { "keywordlike", new MacroDescription("v", "KeywordLike", CONTENTREQUESTTYPES) },
        { "valuelike", new MacroDescription("vv", "ValueLike", CONTENTREQUESTTYPES) },
        { "onlyparents", new MacroDescription("", "OnlyParents", CONTENTREQUESTTYPES) },
        { "basichistory", new MacroDescription("", "BasicHistory", new List<RequestType> { RequestType.activity }) },
        { "notdeleted", new MacroDescription("", "NotDeletedMacro", new List<RequestType> { RequestType.content, RequestType.file, RequestType.page, RequestType.module, RequestType.comment }) }, 
        { "notnull", new MacroDescription("f", "NotNullMacro", Enum.GetValues<RequestType>().ToList()) },
        { "null", new MacroDescription("f", "NullMacro", Enum.GetValues<RequestType>().ToList()) },
        { "usertype", new MacroDescription("i", "UserTypeMacro", new List<RequestType> { RequestType.user }) },
        { "ingroup", new MacroDescription("v", "InGroupMacro", new List<RequestType> { RequestType.user }) },
        //WARN: permission limiting could be very dangerous! Make sure that no matter how the user uses
        //this, they still ONLY get the stuff they're allowed to read!
        { "permissionlimit", new MacroDescription("vfi", "PermissionLimit", new List<RequestType> {
            RequestType.content,
            RequestType.page,
            RequestType.file,
            RequestType.module,
            RequestType.comment
        }) }
    };

    protected List<Type> ViewTypes;
    protected Dictionary<RequestType, Type> StandardViewRequests;

    public QueryBuilder(ILogger<QueryBuilder> logger, IDbTypeInfoService typeInfoService, 
        IMapper mapper, ISearchQueryParser parser, IPermissionService permissionService)
    {
        this.logger = logger;
        this.typeService = typeInfoService;
        this.mapper = mapper;
        this.parser = parser;
        this.permissionService = permissionService;

        var assembly = System.Reflection.Assembly.GetAssembly(GetType()) ?? throw new InvalidOperationException("NO ASSEMBLY FOR QUERYBUILDER???");

        //Pull the view types out, compute the STANDARD mapping of requests to views. Requests that don't have a standard mapping have something custom and don't go through
        //any of the standard codepaths (they do their own thing entirely)
        ViewTypes = assembly.GetTypes().Where(t => String.Equals(t.Namespace, $"{nameof(contentapi)}.{nameof(contentapi.Views)}", StringComparison.Ordinal)).ToList();
        var typeInfos = ViewTypes.Select(x => typeInfoService.GetTypeInfo(x));
        StandardViewRequests = typeInfos.Where(x => x.requestType.HasValue).ToDictionary(
            k => k.requestType ?? throw new InvalidOperationException("How did the HasValue check fail on StandardViewRequest build??"), v => v.type);

        SetupContentComplexFields();
    }

    public void SetupContentComplexFields()
    {
        const string LastPostDateField = nameof(ContentView.lastCommentDate);
        const string LastPostIdField = nameof(ContentView.lastCommentId);
        const string LastRevisionDateField = nameof(ContentView.lastRevisionDate);
        const string LastRevisionIdField = nameof(ContentView.lastRevisionId);
        const string PostCountField = nameof(ContentView.commentCount);
        const string WatchCountField = nameof(ContentView.watchCount);

        //Because of how content is, we need to add these content fields to all things
        foreach(var type in ContentRequestTypes)
        {
            StandardComplexFields.Add((type, LastPostDateField), 
                $"(select createDate from comments where {MainAlias}.id = contentId and {NaturalCommentQuery} order by id desc limit 1) as {LastPostDateField}");
            StandardComplexFields.Add((type, LastPostIdField), 
                $"(select id from comments where {MainAlias}.id = contentId and {NaturalCommentQuery} order by id desc limit 1) as {LastPostIdField}");
            StandardComplexFields.Add((type, LastRevisionDateField), 
                $"(select createDate from content_history where {MainAlias}.id = contentId order by id desc limit 1) as {LastRevisionDateField}");
            StandardComplexFields.Add((type, LastRevisionIdField), 
                $"(select id from content_history where {MainAlias}.id = contentId order by id desc limit 1) as {LastRevisionIdField}");
            StandardComplexFields.Add((type, PostCountField), 
                $"(select count(*) from comments where {MainAlias}.id = contentId and {NaturalCommentQuery}) as {PostCountField}");
            StandardComplexFields.Add((type, WatchCountField), 
                $"(select count(*) from content_watches where {MainAlias}.id = contentId) as {WatchCountField}");
        }
    }

    //NOTE: Even though these might say "0" references, they're all used by the macro system!
    public string KeywordLike(SearchRequestPlus request, string value)
    {
        var typeInfo = typeService.GetTypeInfo<ContentKeyword>();
        return $@"{MainAlias}.id in 
            (select {nameof(ContentKeyword.contentId)} 
             from {typeInfo.modelTable} 
             where {nameof(ContentKeyword.value)} like {value}
            )";
    }

    public string ValueLike(SearchRequestPlus request, string key, string value)
    {
        var typeInfo = typeService.GetTypeInfo<ContentValue>();
        return $@"{MainAlias}.id in 
            (select {nameof(ContentValue.contentId)} 
             from {typeInfo.modelTable} 
             where {nameof(ContentValue.key)} like {key} 
               and {nameof(ContentValue.value)} like {value}
            )";
    }

    public string OnlyParents(SearchRequestPlus request)
    {
        var typeInfo = typeService.GetTypeInfo<Content>();
        return $@"{MainAlias}.id in 
            (select {nameof(Content.parentId)} 
             from {typeInfo.modelTable} 
             group by {nameof(Content.parentId)}
            )";
    }

    public string BasicHistory(SearchRequestPlus request)
    {
        var typeInfo = typeService.GetTypeInfo<Content>();
        return $@"{MainAlias}.contentId in 
            (select {nameof(Content.id)} 
             from {typeInfo.modelTable} 
             where internalType = {(int)InternalContentType.page}
             and deleted = 0
            )";
    }

    public string InGroupMacro(SearchRequestPlus request, string group)
    {
        var typeInfo = typeService.GetTypeInfo<UserRelation>();
        return $@"{MainAlias}.id in 
            (select {nameof(Db.UserRelation.userId)} 
             from {typeInfo.modelTable} 
             where {nameof(Db.UserRelation.relatedId)} = {group}
            )";
    }

    public string NotNullMacro(SearchRequestPlus request, string field) { return $"{field} IS NOT NULL"; }
    public string NullMacro(SearchRequestPlus request, string field) { return $"{field} IS NULL"; }
    public string UserTypeMacro(SearchRequestPlus request, string type) { return EnumMacroSearch<UserType>(type); }
    public string NotDeletedMacro(SearchRequestPlus request) { return "deleted = 0"; }

    //NOTE: Even though these might say "0" references, they're all used by the macro system!
    //For now, this is JUST read limit!!
    public string PermissionLimit(SearchRequestPlus request, string requesters, string idField, string type)
    {
        var typeInfo = typeService.GetTypeInfo<ContentPermission>();
        var checkCol = permissionService.ActionToColumn(permissionService.StringToAction(type));

        //Note: we're checking createUserId against ALL requester values they gave us! This is OK, because the
        //additional values are things like 0 or their groups, and groups can't create content
        return $@"({MainAlias}.{idField} in 
            (select {nameof(ContentPermission.contentId)} 
             from {typeInfo.modelTable} 
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

        return reqplus;
    }

    //public bool IsSimpleField(string fieldName, SearchRequestPlus r, out string realFieldname)
    //{
    //    realFieldname = "";

    //    var result = !StandardComplexFields.ContainsKey((r.requestType, fieldName)) && !string.IsNullOrWhiteSpace(r.typeInfo.fields[fieldName].realDbColumn);

    //    if(result)
    //        realFieldname = r.typeInfo.fields[fieldName].realDbColumn ?? throw new InvalidOperationException("Somehow, got null db column even though we checked for null!");
    //    
    //    return result;
    //}

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
    public string StandardFieldRemap(string fieldName, SearchRequestPlus r)
    {
        //Our personal field modifiers always override all
        if (StandardComplexFields.ContainsKey((r.requestType, fieldName)))
        {
            return StandardComplexFields[(r.requestType, fieldName)];
        }
        else //Otherwise, just use what is defined by the type info
        {
            var c = r.typeInfo.fields[fieldName].realDbColumn;

            if(string.IsNullOrWhiteSpace(c))
                throw new InvalidOperationException($"Can't remap field '{fieldName}': no complex field mapping defined in the query builder AND no real db column defined on the field! Computed: {r.typeInfo.fields[fieldName].computed}");
            else if (c == fieldName)
                return c;
            else
                return $"{c} AS {fieldName}";
        }
    }

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
            if (!r.typeInfo.fields.ContainsKey(field))
                throw new ArgumentException($"Unknown field {field} in request {r.name}");
        
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
    public string ParseField(string field, SearchRequestPlus request)
    {
        if(!request.typeInfo.fields.ContainsKey(field))
            throw new ArgumentException($"Field '{field}' not found in type '{request.type}'({request.name})!");

        if(!request.typeInfo.fields[field].queryable)
            throw new ArgumentException($"Field '{field}' not searchable in type '{request.type}'({request.name})!");

        //I don't know what to do about computed fields just yet, so they'll just pass through... we'll see how that works out
        //(there don't appear to be ANY computed fields just yet)

        //Oops, sometimes a field might not be part of the request but we're querying against it. This requires special things
        if(!request.requestFields.Contains(field))
        {
            if(StandardComplexFields.ContainsKey((request.requestType, field)))
                throw new ArgumentException($"Field '{field}' is a complex field for type '{request.type}' and must be included in the requested fieldlist ({request.name})");
            else if(string.IsNullOrWhiteSpace(request.typeInfo.fields[field].realDbColumn))
                throw new InvalidOperationException($"Field '{field}' in type '{request.type}' doesn't map to any database data and does not have a complex field definition, which means it is misconfigured in the API. ({request.name})");
            else //If we get to here, we have a renamed field that was NOT requested in the fieldlist, so we need to give the REAL database name for the parse
                return request.typeInfo.fields[field].realDbColumn ?? throw new InvalidOperationException("Somehow, realDbColumn was null even after a null check!");
        }

        return field;
    }

    public string ParseValue(string value, SearchRequestPlus request, Dictionary<string, object> parameters)
    {
        var realValName = value.TrimStart('@');
        if (!parameters.ContainsKey(realValName))
        {
            var dotParts = realValName.Split(".".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            var newName = realValName.Replace(".", "_");

            if (dotParts.Length == 1)
                throw new ArgumentException($"Value {value} not found for request {request.name}");

            //For now, let's just assume when linking, they're going one deep
            if (dotParts.Length != 2)
                throw new ArgumentException($"For now, can only access 1 layer deep in results, fix {value} in {request.name}");

            var resultName = dotParts[0];
            var resultField = dotParts[1];

            if (!parameters.ContainsKey(resultName))
                throw new ArgumentException($"No base link {resultName} for {value} in {request.name}");

            var testList = parameters[resultName];

            if (!(testList is IEnumerable<IDictionary<string, object>>))
                throw new ArgumentException($"Base link {resultName} improper type for {value} in {request.name}");

            var valList = (IEnumerable<IDictionary<string, object>>)testList;

            if (valList.Count() == 0)
            {
                //There's no results, so most things will fail, but whatever. In this case,
                //we don't care about the field
                parameters.Add(newName, new List<object>());
                return $"@{newName}";
            }

            if (!valList.First().ContainsKey(resultField))
                throw new ArgumentException($"Link result {resultName} has no field {resultField} for {value} in {request.name}");

            //OK we finally have it, let's go
            parameters.Add(newName, valList.Select(x => x[resultField]).Where(x => x != null));
            return $"@{newName}";
        }
        return value;
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
        var fieldSelect = r.requestFields.Where(x => !r.typeInfo.fields[x].computed).Select(x => StandardFieldRemap(x, r)).ToList();

        queryStr.Append("SELECT ");
        queryStr.Append(string.Join(",", fieldSelect));
        queryStr.Append(" FROM ");
        queryStr.Append(r.typeInfo.modelTable ?? throw new InvalidOperationException($"Standard select {r.type} doesn't map to database table in request {r.name}!"));
        queryStr.Append($" AS {MainAlias} ");
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
            //queryStr.Append($"ORDER BY {order} ");
            //var order = r.order;

            for(int i = 0; i < orders.Length; i++)
            {
                var order = orders[i];
                var descending = false;

                if (r.order.EndsWith(DescendingAppend))
                {
                    descending = true;
                    order = r.order.Substring(0, r.order.Length - DescendingAppend.Length);
                }

                if (!r.typeInfo.fields.ContainsKey(order)) //queryableFields.Contains(order))
                    throw new ArgumentException($"Unknown order field {order} for request {r.name}");
                
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

        if(StandardViewRequests.ContainsKey(reqplus.requestType))
        {
            //Generate "select from"
            AddStandardSelect(queryStr, reqplus);

            //Generate "where (x)"
            var query = CreateStandardQuery(queryStr, reqplus, parameters);    
            query = CombineQueryClause(query, StandardSearchModifiers.GetValueOrDefault(reqplus.requestType, ""));
            if (!string.IsNullOrWhiteSpace(query))
                queryStr.Append($"WHERE {query} ");

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

        return result;
    }
}