using System.Collections.Generic;
using System.Linq;

namespace contentapi.Services.Views
{
    /// <summary>
    /// The general source of most view translation
    /// </summary>
    /// <typeparam name="V"></typeparam>
    /// <typeparam name="T"></typeparam>
    public interface IViewConverter<V, T>
    {
        V ToView(T basic);
        T FromView(V view);

    }
}