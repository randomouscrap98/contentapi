using System.Collections.Generic;
using System.Linq;

namespace contentapi.Services.Mapping
{
    /// <summary>
    /// Simple conversion between views and a type T
    /// </summary>
    /// <typeparam name="V"></typeparam>
    /// <typeparam name="T"></typeparam>
    public interface IViewConverter<V, T>
    {
        V ToView(T basic);
        T FromView(V view);
    }
}