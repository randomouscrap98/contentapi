using System.Data;

namespace contentapi;

/// <summary>
///  Doesn't build the WHOLE query, just parts of it.
/// </summary>
public interface IQueryPartBuilder
{
    List<string> GetSelectFields<T>(SearchRequest request);

    //NO async unless you absolutely need it! Always prefer inner query!
    //Note: values may not contain EXACTLY what is needed! In that case, add it once you know the format!
    //This QueryPartBuilder has to know the formats of objects anyway, so it can add those to its cache, then
    //dive deep to find the actual value to place into the emitter.
    void GenerateWhereSequence<T>(IDbCommand command, SearchRequest request,
        Dictionary<string, object> values, Dictionary<string, Action<IDbCommand, WhereExpression>> emitters);

    //Need the type here to ensure the order field makes sense...
    void GenerateOrderLimitFinalize<T>(IDbCommand command, SearchRequest request);
}