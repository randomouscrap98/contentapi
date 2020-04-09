using System;
using System.Collections.Generic;
using System.Linq;

namespace contentapi.Services.Extensions
{
    public static class IEnumerableExtensions
    {
        /// <summary>
        /// Get a single element, return null if none, or fail on multiple (throw exception)
        /// </summary>
        /// <param name="list"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static T OnlySingle<T>(this IEnumerable<T> list)
        {
            if(list.Count() > 1)
                throw new InvalidOperationException("Multiple values found; expected 1");
            
            return list.FirstOrDefault();
        }
    }
}