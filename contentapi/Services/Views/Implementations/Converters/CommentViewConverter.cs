using System;
using System.Linq;
using contentapi.Services.Constants;
using contentapi.Views;
using Randomous.EntitySystem;

namespace contentapi.Services.Views.Implementations
{
    public class CommentViewConverter : BaseViewConverter, IViewConverter<CommentView, EntityRelationPackage>
    {
        public CommentView ToViewSimple(EntityRelation relation)
        {
            var view = new CommentView();
            ApplyToViewBasic(relation, view);
            view.createUserId = -relation.entityId2;
            view.content = relation.value;
            view.parentId = relation.entityId1;

            //Assume (bad assume!) that these are OK values... we don't know if edit is even supported?
            view.editUserId = view.createUserId;
            view.editDate = view.createDate;

            return view;
        }

        public EntityRelation FromViewSimple(CommentView view)
        {
            var relation = new EntityRelation();
            ApplyFromViewBasic(view, relation);
            relation.type = Keys.CommentHack;
            relation.value = view.content;
            relation.entityId1 = view.parentId;
            relation.entityId2 = -view.createUserId;
            return relation;
        }

        public CommentView ToView(EntityRelationPackage package)
        {
            var view = ToViewSimple(package.Main);
            var orderedRelations = package.Related.OrderBy(x => x.id);
            var lastEdit = orderedRelations.LastOrDefault(x => x.type.StartsWith(Keys.CommentHistoryHack));
            var last = orderedRelations.LastOrDefault();

            if(lastEdit != null)
            {
                view.editDate = (DateTime)lastEdit.createDateProper();
                view.editUserId = -lastEdit.entityId2;
            }

            view.deleted = last != null && last.type.StartsWith(Keys.CommentDeleteHack);

            return view;
        }

        public EntityRelationPackage FromView(CommentView view)
        {
            throw new InvalidOperationException("This function makes no sense for comments, they are a hack!");
        }
    }
}