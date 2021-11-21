
using System.Collections.Generic;
using System.Linq;

namespace contentapi.Services.Constants
{
    public static class Actions
    {
        //This COULD be manipulated buuuuuut oh well.
        /// <summary>
        /// Keys are single-letter action, values are keys
        /// </summary>
        /// <typeparam name="string"></typeparam>
        /// <typeparam name="string"></typeparam>
        /// <returns></returns>
        public static readonly Dictionary<string, string> ActionMap = new Dictionary<string, string>()
        {
            {"c", Keys.CreateAction},
            {"r", Keys.ReadAction},
            {"u", Keys.UpdateAction},
            {"d", Keys.DeleteAction}
        };

        //Just a reverse map
        /// <summary>
        /// Keys are keys, valuesa re single-letter action
        /// </summary>
        /// <param name="x.Value"></param>
        /// <param name="y.Key"></param>
        /// <returns></returns>
        public static readonly Dictionary<string, string> KeyMap = ActionMap.ToDictionary(x => x.Value, y => y.Key);
    }
}