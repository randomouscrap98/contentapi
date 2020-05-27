using System;
using System.Collections.Generic;

namespace contentapi
{
    public class IgnoreCompareAttribute : Attribute { }

    public class CompareBase
    {
        /// <summary>
        /// This is a SLOW comparison function but I don't care: comparison is probably only performed on unit tests.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        protected virtual bool EqualsSelf(object obj)
        {
            var type = obj.GetType();

            //only public properties, that's fine, these are all views. ALSO no lists/dictionaries, those are on YOU to compare!
            var properties = type.GetProperties();
            
            foreach(var property in type.GetProperties())
            {
                if(Attribute.IsDefined(property, typeof(IgnoreCompareAttribute)))
                    continue;

                if(property.PropertyType.IsGenericType)
                {
                    var generic = property.PropertyType.GetGenericTypeDefinition();
                    if(generic.IsAssignableFrom(typeof(List<>)) || generic.IsAssignableFrom(typeof(Dictionary<,>)))
                        continue;
                }

                var thisVal = property.GetValue(this);
                var otherVal = property.GetValue(obj);

                //Can't do an equals on null. Must be careful!
                if(thisVal == null)
                {
                    if(otherVal != null)
                        return false;
                }
                else
                {
                    if(!thisVal.Equals(otherVal))
                        return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            if(obj != null && this.GetType().Equals(obj.GetType()))
                return EqualsSelf(obj);
            else
                return false;
        }

        public override int GetHashCode() 
        { 
            return base.GetHashCode(); 
        }
    }
}