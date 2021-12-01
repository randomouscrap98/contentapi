namespace contentapi.Search;

public enum MacroArgumentType { field, value, fieldImmediate }

public class MacroDescription
{
    public List<MacroArgumentType> argumentTypes = new List<MacroArgumentType>();
    public System.Reflection.MethodInfo macroMethod;
    public List<RequestType> allowedTypes;

    public MacroDescription(string argTypes, string methodName, List<RequestType> allowedTypes)
    {
        this.allowedTypes = allowedTypes;
        foreach (var c in argTypes)
        {
            if (c == 'v')
                argumentTypes.Add(MacroArgumentType.value);
            else if (c == 'f')
                argumentTypes.Add(MacroArgumentType.field);
            else if (c == 'i')
                argumentTypes.Add(MacroArgumentType.fieldImmediate);
            else
                throw new InvalidOperationException($"Unknown arg type {c}");
        }

        macroMethod = typeof(QueryBuilder).GetMethod(methodName) ??
            throw new InvalidOperationException($"Couldn't find macro definition {methodName}");
    }
}
