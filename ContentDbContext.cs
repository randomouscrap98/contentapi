using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using contentapi.Models;
using Microsoft.EntityFrameworkCore;

namespace contentapi
{
    //Every single "MyContext" will essentially look the same: you just add 
    //the available fields you can search.
    public class ContentDbContext : DbContext//, IDataRetrieval
    {
        public ContentDbContext(DbContextOptions<ContentDbContext> options) : base(options) { }

        //public DbSet<User> Users {get; set;}
        //public DbSet<Category> Categories {get;set;}
        //public DbSet<Content> Content {get;set;}
        //public DbSet<ContentAccess> ContentAccesses {get;set;}

        public IQueryable<T> GetAll<T>(params long[] ids) where T : GenericModel 
        {
            //var set = Set<T>();
            //IQueryable<T> result = set;
            var set = Set<T>();
            IQueryable<T> result = set;

            if(ids != null && ids.Length > 0)
                result = set.Where(x => ids.Contains(x.id));

            return result;
        }

        public async Task<T> GetSingleAsync<T>(long id) where T : GenericModel
        {
            return await GetAll<T>(id).FirstAsync();
        }

        //public IQueryable<T> GetList<T>(IEnumerable<long> ids) where T : GenericModel 
        //{
        //    return GetAll<T>().FindAsync(x => ids.Contains(x.id));
        //}

        //public IQueryable<T> GetRaw<T>() where T : GenericModel
        //{
        //    return Set<T>();
        //}
    }
}