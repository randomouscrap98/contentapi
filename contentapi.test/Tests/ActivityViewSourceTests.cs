using System.Linq;
using contentapi.Services.Constants;
using contentapi.Services.Extensions;
using contentapi.Services.Implementations;
using Randomous.EntitySystem;
using Xunit;

namespace contentapi.test
{
   public class ActivityViewSourceTests : ReadTestBase //ServiceTestBase<ActivityViewSource>
   {
      protected ActivityViewSource service;
      protected IEntityProvider provider;
      protected ReadTestUnit unit;

      public ActivityViewSourceTests()
      {
          service = CreateService<ActivityViewSource>();
          provider = CreateService<IEntityProvider>();
          unit = CreateUnitAsync().Result;
      }

      [Fact]
      public void SimpleMakeActivity()
      {
         //Just make sure it doesn't throw a fit
         var relation = service.MakeActivity(NewEntity(5), 8, Keys.CreateAction);
         Assert.NotNull(relation);
         Assert.True(relation.entityId1 != 0); //The relation should map two things together DEFINITELY
         Assert.True(relation.entityId2 != 0);
      }

      [Fact]
      public void SimpleFindActivity()
      {
         var result = service.SimpleSearchAsync(new ActivitySearch()).Result;

         //Unit has 4 activities: two user creates and two contents
         Assert.Equal(4, result.Count);
         Assert.Equal(2, result.Count(x => x.type == Keys.TypeNames[Keys.ContentType]));
         Assert.Equal(2, result.Count(x => x.type == Keys.TypeNames[Keys.UserType]));
      }

      [Fact]
      public void FindOnlyType()
      {
         var search = new ActivitySearch() { ActivityType = Keys.TypeNames[Keys.ContentType]};

         var result = service.SimpleSearchAsync(search).Result;
         Assert.Equal(2, result.Count);
         Assert.Equal(2, result.Count(x => x.type == Keys.TypeNames[Keys.ContentType]));

         search.ActivityType = Keys.TypeNames[Keys.UserType];

         result = service.SimpleSearchAsync(search).Result;
         Assert.Equal(2, result.Count);
         Assert.Equal(2, result.Count(x => x.type == Keys.TypeNames[Keys.UserType]));
      }

      [Fact]
      public void FindContentType()
      {
         var search = new ActivitySearch() { ContentType = unit.commonContent.type };

         var result = service.SimpleSearchAsync(search).Result;
         Assert.Equal(2, result.Count);
         Assert.Equal(2, result.Count(x => x.contentType == unit.commonContent.type));

         search.ContentType = "somethingRandom";

         result = service.SimpleSearchAsync(search).Result;
         Assert.Empty(result);
      }

      [Fact]
      public void FindNotType()
      {
         var search = new ActivitySearch();
         search.NotActivityTypes.Add(Keys.TypeNames[Keys.UserType]);

         var result = service.SimpleSearchAsync(search).Result;
         Assert.Equal(2, result.Count);
         Assert.Equal(2, result.Count(x => x.type != Keys.TypeNames[Keys.UserType]));

         search.NotActivityTypes.Add(Keys.TypeNames[Keys.ContentType]);

         result = service.SimpleSearchAsync(search).Result;
         Assert.Empty(result);

         search.NotActivityTypes.Remove(Keys.TypeNames[Keys.UserType]);
         result = service.SimpleSearchAsync(search).Result;
         Assert.Equal(2, result.Count);
         Assert.Equal(2, result.Count(x => x.type != Keys.TypeNames[Keys.ContentType]));
      }

      [Fact]
      public void FindNotContentType()
      {
         var search = new ActivitySearch();
         search.NotContentTypes.Add("somethingRandom");

         var result = service.SimpleSearchAsync(search).Result;
         Assert.Equal(4, result.Count);
         Assert.Equal(4, result.Count(x => x.contentType != "somethingRandom"));

         search.NotContentTypes.Add(unit.commonContent.type);

         result = service.SimpleSearchAsync(search).Result;
         Assert.Equal(2, result.Count);
         Assert.Equal(2, result.Count(x => x.contentType != unit.commonContent.type));
      }

      [Fact]
      public void Regression_DoubleType()
      {
         var relation = service.MakeActivity(NewEntity(5, Keys.ContentType + "@wow.yeah"), 8, Keys.CreateAction, "myextra");
         var activity = service.ToView(relation);

         Assert.Equal(Keys.TypeNames[Keys.ContentType], activity.type);
         Assert.Equal("@wow.yeah", activity.contentType);
      }

      [Fact]
      public void Regression_DoubleTypeEmpty()
      {
         var relation = service.MakeActivity(NewEntity(5, Keys.FileType), 8, Keys.CreateAction, "myextra");
         var activity = service.ToView(relation);

         Assert.Equal(Keys.TypeNames[Keys.FileType], activity.type);
         Assert.True(string.IsNullOrWhiteSpace(activity.contentType));
      }
   }
}