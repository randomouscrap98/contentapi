using System;
using contentapi.Views;
using Randomous.EntitySystem;

namespace contentapi.Services.Views.Extensions
{
    /// <summary>
    /// The most basic mapper: everything has an ID and a create date
    /// </summary>
    public static class BaseViewExtensions
    {
        public static void ApplyToBaseView<V,T>(this IViewConverter<V,T> converter, EntityBase entityBase, IBaseView view)
        {
            view.id = entityBase.id;
            view.createDate = (DateTime)entityBase.createDateProper();
        }

        public static void ApplyFromBaseView<V,T>(this IViewConverter<V,T> converter, IBaseView view, EntityBase entityBase)
        {
            entityBase.id = view.id;
            entityBase.createDate = view.createDate;
        }
    }
}