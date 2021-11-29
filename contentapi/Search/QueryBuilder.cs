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
    protected ITypeInfoService typeService;
    protected ISearchQueryParser parser;
    protected IMapper mapper;

    public const string MainAlias = "main";
    public const string DescendingAppend = "_desc";
    
    //Should this be configurable? I don't care for now
    protected static readonly Dictionary<RequestType, Type> StandardViewRequests = new Dictionary<RequestType, Type> {
        { RequestType.user, typeof(UserView) },
        { RequestType.comment, typeof(CommentView) },
        { RequestType.content, typeof(ContentView) },
        { RequestType.page, typeof(PageView) },
        { RequestType.module, typeof(ModuleView) },
        { RequestType.file, typeof(FileView) },
        { RequestType.activity, typeof(ActivityView) },
        { RequestType.watch, typeof(WatchView) }
    };

    protected static readonly List<RequestType> CONTENTREQUESTTYPES = new List<RequestType>()
    {
        RequestType.content, RequestType.file, RequestType.page, RequestType.module
    };

    public List<RequestType> ContentRequestTypes => CONTENTREQUESTTYPES;

    public const string LastPostDateField = nameof(ContentView.lastCommentDate);
    public const string LastPostIdField = nameof(ContentView.lastCommentId);
    public const string QuantizationField = nameof(FileView.quantization);
    public const string DescriptionField = nameof(ModuleView.description);
    public const string InternalTypeField = nameof(ContentView.internalType);
    public const string PostCountField = nameof(ContentView.commentCount);
    public const string WatchCountField = nameof(ContentView.watchCount);
    public const string ActionField = nameof(ActivityView.action);

    //These fields are too difficult to modify with the attributes, so we do it in code here
    protected readonly Dictionary<(RequestType, string),string> StandardModifiedFields = new Dictionary<(RequestType, string), string> {
        { (RequestType.file, QuantizationField), $"(select value from content_values where {MainAlias}.id = contentId and key='{QuantizationField}' limit 1) as {QuantizationField}" },
        { (RequestType.module, DescriptionField), $"(select value from content_values where {MainAlias}.id = contentId and key='{DescriptionField}' limit 1) as {DescriptionField}" },
        { (RequestType.user, "registered"), $"(registrationKey IS NULL) as registered" },
        { (RequestType.activity, ActionField), EnumToCase<UserAction>(ActionField, ActionField)}
    };

    //Some searches can easily be modified afterwards with constants, put that here.
    protected readonly Dictionary<RequestType, string> StandardSearchModifiers = new Dictionary<RequestType, string>()
    {
       { RequestType.file, $"internalType = {(int)InternalContentType.file}" },
       { RequestType.module, $"internalType = {(int)InternalContentType.module}" },
       { RequestType.page, $"internalType = {(int)InternalContentType.page}" },
       { RequestType.comment, $"module IS NULL" } 
    };

    protected enum MacroArgumentType { field, value }

    protected class MacroDescription
    {
        public List<MacroArgumentType> argumentTypes = new List<MacroArgumentType>();
        public System.Reflection.MethodInfo macroMethod;
        public List<RequestType> allowedTypes;

        public MacroDescription(string argTypes, string methodName, List<RequestType> allowedTypes)
        {
            this.allowedTypes = allowedTypes;
            foreach(var c in argTypes)
            {
                if(c == 'v')
                    argumentTypes.Add(MacroArgumentType.value);
                else if(c == 'f')
                    argumentTypes.Add(MacroArgumentType.field);
                else
                    throw new InvalidOperationException($"Unknown arg type {c}");
            }

            macroMethod = typeof(QueryBuilder).GetMethod(methodName) ?? 
                throw new InvalidOperationException($"Couldn't find macro definition {methodName}");
        }
    }

    protected readonly Dictionary<string, MacroDescription> StandardMacros = new Dictionary<string, MacroDescription>()
    {
        { "keywordlike", new MacroDescription("v", "KeywordLike", CONTENTREQUESTTYPES) },
        { "valuelike", new MacroDescription("vv", "ValueLike", CONTENTREQUESTTYPES) },
        { "onlyparents", new MacroDescription("", "OnlyParents", CONTENTREQUESTTYPES) },
        { "basichistory", new MacroDescription("", "BasicHistory", new List<RequestType> { RequestType.activity })},
        { "notnull", new MacroDescription("f", "NotNullMacro", Enum.GetValues<RequestType>().ToList()) },
        { "null", new MacroDescription("f", "NullMacro", Enum.GetValues<RequestType>().ToList()) },
        //WARN: permission limiting could be very dangerous! Make sure that no matter how the user uses
        //this, they still ONLY get the stuff they're allowed to read!
        { "permissionlimit", new MacroDescription("vf", "PermissionLimit", new List<RequestType> {
            RequestType.content,
            RequestType.page,
            RequestType.file,
            RequestType.module,
            RequestType.comment
        }) }
    };

    public QueryBuilder(ILogger<QueryBuilder> logger, ITypeInfoService typeInfoService, 
        IMapper mapper, ISearchQueryParser parser)
    {
        this.logger = logger;
        this.typeService = typeInfoService;
        this.mapper = mapper;
        this.parser = parser;

        //Because of how content is, we need to add these content fields to all things
        foreach(var type in ContentRequestTypes)
        {
            StandardModifiedFields.Add((type, InternalTypeField), 
                EnumToCase<InternalContentType>(nameof(Db.Content.internalType), InternalTypeField));
            StandardModifiedFields.Add((type, LastPostDateField), 
                $"(select createDate from comments where {MainAlias}.id = contentId order by id desc limit 1) as {LastPostDateField}");
            StandardModifiedFields.Add((type, LastPostIdField), 
                $"(select id from comments where {MainAlias}.id = contentId order by id desc limit 1) as {LastPostIdField}");
            StandardModifiedFields.Add((type, PostCountField), 
                $"(select count(*) from comments where {MainAlias}.id = contentId) as {PostCountField}");
            StandardModifiedFields.Add((type, WatchCountField), 
                $"(select count(*) from content_watches where {MainAlias}.id = contentId) as {WatchCountField}");
        }
    }

    public static string EnumToCase<T>(string fieldName, string outputName) where T : struct, System.Enum 
    {
        var values = Enum.GetValues<T>();
        var whens = values.Select(x => $"WHEN {Unsafe.As<T, int>(ref x)} THEN '{x.ToString()}'");
        return $"CASE {fieldName} {string.Join(" ", whens)} ELSE 'unknown' END as {outputName}";
    }

    public string KeywordLike(SearchRequestPlus request, string value)
    {
        var typeInfo = typeService.GetTypeInfo<ContentKeyword>();
        return $@"{MainAlias}.id in 
            (select {nameof(ContentKeyword.contentId)} 
             from {typeInfo.database} 
             where {nameof(ContentKeyword.value)} like {value}
            )";
    }

    public string ValueLike(SearchRequestPlus request, string key, string value)
    {
        var typeInfo = typeService.GetTypeInfo<ContentValue>();
        return $@"{MainAlias}.id in 
            (select {nameof(ContentValue.contentId)} 
             from {typeInfo.database} 
             where {nameof(ContentValue.key)} like {key} 
               and {nameof(ContentValue.value)} like {value}
            )";
    }

    public string OnlyParents(SearchRequestPlus request)
    {
        var typeInfo = typeService.GetTypeInfo<Content>();
        return $@"{MainAlias}.id in 
            (select {nameof(Content.parentId)} 
             from {typeInfo.database} 
             group by {nameof(Content.parentId)}
            )";
    }

    public string BasicHistory(SearchRequestPlus request)
    {
        var typeInfo = typeService.GetTypeInfo<Content>();
        return $@"{MainAlias}.contentId in 
            (select {nameof(Content.id)} 
             from {typeInfo.database} 
             where internalType = {(int)InternalContentType.page}
             and deleted = 0
            )";
    }

    public string NotNullMacro(SearchRequestPlus request, string field) { return $"{field} IS NOT NULL"; }
    public string NullMacro(SearchRequestPlus request, string field) { return $"{field} IS NULL"; }

    //For now, this is JUST read limit!!
    public string PermissionLimit(SearchRequestPlus request, string requester, string idField)
    {
        var typeInfo = typeService.GetTypeInfo<ContentPermission>();
        return $@"{MainAlias}.{idField} in 
            (select {nameof(ContentPermission.contentId)} 
             from {typeInfo.database} 
             where {nameof(ContentPermission.userId)} in (0,{requester})
               and {nameof(ContentPermission.read)} = 1
            )";
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

        //Now do some pre-parsing and data retrieval. If one of these fail, it's good that it
        //fails early (although it will only fail if me, the programmer, did something wrong,
        //not if the user supplies a weird request)
        var reqplus = mapper.Map<SearchRequestPlus>(request);
        reqplus.requestType = Enum.Parse<RequestType>(reqplus.type); //I know this will succeed
        reqplus.typeInfo = typeService.GetTypeInfo(StandardViewRequests[reqplus.requestType]);
        reqplus.requestFields = ComputeRealFields(reqplus);

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
    public string StandardFieldRemap(string fieldName, SearchRequestPlus r)
    {
        //Our personal field modifiers always override all
        if (StandardModifiedFields.ContainsKey((r.requestType, fieldName)))
        {
            return StandardModifiedFields[(r.requestType, fieldName)];
        }
        else if (r.typeInfo.fieldRemap.ContainsKey(fieldName))
        {
            //Can't return a blank "as" for a null field remap. Null/empty remaps are
            //fields which you're allowed to query for, but which should not be output in
            //the selector (hence the field mapped to "nothing")
            if(string.IsNullOrWhiteSpace(r.typeInfo.fieldRemap[fieldName]))
                return "";
            else
                return $"{r.typeInfo.fieldRemap[fieldName]} AS {fieldName}";
        }
        //Just a basic fieldname replacement
        else
        {
            return fieldName;
        }
    }

    public List<string> ComputeRealFields(SearchRequestPlus r)
    {
        var fields = r.fields.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToList(); 

        //Redo fieldlist if they asked for special formats
        if (r.fields == "*")
            fields = new List<string>(r.typeInfo.queryableFields);
        
        //Check for bad fields
        foreach (var field in fields)
            if (!r.typeInfo.queryableFields.Contains(field))
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

    public string ParseField(string field, SearchRequestPlus request)
    {
        if(!request.typeInfo.searchableFields.Contains(field))
            throw new ArgumentException($"Field '{field}' not searchable yet in type '{request.type}'({request.name})!");

        //Oops, this is a dangerous field, we can't just use it without requesting it because
        //it's COMPUTED
        if(StandardModifiedFields.ContainsKey((request.requestType, field)) || 
            request.typeInfo.fieldRemap.ContainsKey(field))
        {
            if(!request.requestFields.Contains(field))
                throw new ArgumentException($"Field '{field}' is a computed field for type '{request.type}' and must be included in the retrieved fieldlist ({request.name})");
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
            else
            {
                throw new InvalidOperationException($"Unknown macro argument type {macDef.argumentTypes[i]} in request {request.name}");
            }
        }

        //At this point, we have the macro function info, so we can just call it
        return (string)(macDef.macroMethod.Invoke(this, argVals.ToArray()) ?? 
            throw new InvalidOperationException($""));
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
        catch (ArgumentException)
        {
            //Skip argument exceptions, we already expect those.
            throw;
        }
        catch (Exception ex)
        {
            //Convert to argument exception so the user knows what's up. Nothing that happens here
            //is due to a database or other "internal" server error (other than stupid messups on my part)
            logger.LogWarning($"Exception during query parse: {ex}");
            throw new ArgumentException($"Parse  error: {ex.Message}");
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
        var fieldSelect = r.requestFields.Select(x => StandardFieldRemap(x, r)).Where(x => !string.IsNullOrEmpty(x)).ToList();

        queryStr.Append("SELECT ");
        queryStr.Append(string.Join(",", fieldSelect));
        queryStr.Append(" FROM ");
        queryStr.Append(r.typeInfo.database ?? throw new InvalidOperationException($"Standard select {r.type} doesn't map to database table in request {r.name}!"));
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
            var order = r.order;
            var descending = false;

            if(r.order.EndsWith(DescendingAppend))
            {
                descending = true;
                order = r.order.Substring(0, r.order.Length - DescendingAppend.Length);
            }

            if(!r.typeInfo.queryableFields.Contains(order))
                throw new ArgumentException($"Unknown order field {order} for request {r.name}");

            //Can't parameterize the column... inserting directly; scary
            queryStr.Append($"ORDER BY {order} ");

            if(descending)
                queryStr.Append("DESC ");
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
                result.types.Add(type.ToString(), new {
                    getfields = typeInfo.queryableFields,
                    searchfields = typeInfo.searchableFields
                });

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