using System;
using contentapi.Views;
using Randomous.EntitySystem;

namespace contentapi.Services.Views.Implementations
{
    /// <summary>
    /// The most basic mapper: everything has an ID and a create date
    /// </summary>
    public class BaseViewConverter
    {
        public void ApplyToBaseView(EntityBase entityBase, IBaseView view)
        {
            view.id = entityBase.id;
            view.createDate = (DateTime)entityBase.createDateProper();
        }

        public void ApplyFromBaseView(IBaseView view, EntityBase entityBase)
        {
            entityBase.id = view.id;
            entityBase.createDate = view.createDate;
        }
    }
}