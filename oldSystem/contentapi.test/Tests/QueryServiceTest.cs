using System;
using System.Collections.Generic;
using System.Linq;
using contentapi.Models;
using contentapi.Services;
using contentapi.test.Overrides;
using Xunit;

namespace contentapi.test
{
    public class QueryBase : ControllerTestBase<OpenController>
    {
        public ControllerInstance<OpenController> Instance = null;
        public List<CategoryView> InsertedViews = null;
    }

    public class QueryServiceTest : IClassFixture<QueryBase>
    {
        private QueryBase qbase;

        public QueryServiceTest(QueryBase qbase)
        {
            this.qbase = qbase; 

            if(qbase.Instance == null)
                qbase.Instance = qbase.GetInstance(true);
            if(qbase.InsertedViews == null)
                qbase.InsertedViews = qbase.Instance.Controller.InsertRandom(100);

            Assert.True(100 <= qbase.Instance.QueryService.DefaultResultCount);

        }

        protected List<CategoryEntity> GetQuery(CollectionQuery query) //, int count = 100)
        {
            var queryViews = qbase.Instance.QueryService.ApplyQuery(qbase.Instance.Context.CategoryEntities, query).ToList();
            return queryViews;
        }

        [Fact]
        public void TestBasicQuery()
        {
            var created = GetQuery(new CollectionQuery());
            Assert.Equal(qbase.InsertedViews.Count, created.Count);
        }

        [Fact]
        public void TestOrdering()
        {
            var created = GetQuery(new CollectionQuery() { sort = qbase.Instance.QueryService.IdSort, order = qbase.Instance.QueryService.DescendingOrder });

            for(int i = 1; i < created.Count; i++)
                Assert.True(created[i - 1].entityId > created[i].entityId);
        }

        [Theory]
        [InlineData(0, 10)]
        [InlineData(10, 10)]
        [InlineData(90, 10)]
        [InlineData(0, 100)]
        [InlineData(23, 7)]
        [InlineData(99, 1)]
        public void TestOffsetCount(int offset, int count)
        {
            var query = new CollectionQuery() { offset = offset, count = count };
            var created = GetQuery(query);
            Assert.True(qbase.InsertedViews.Skip(offset).Take(count).Select(x => x.id).SequenceEqual(created.Select(x => x.entityId)));
        }
    }
}