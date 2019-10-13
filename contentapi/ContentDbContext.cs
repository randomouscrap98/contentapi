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
    public class ContentDbContext : DbContext
    {
        public ContentDbContext(DbContextOptions<ContentDbContext> options) : base(options) { }

        public DbSet<User> Users {get;set;}
        public DbSet<Entity> Entities {get;set;}
        public DbSet<EntityAccess> EntityAccess {get;set;}
        public DbSet<EntityLog> EntityLogs {get;set;}

        //public DbSet<Category> Categories {get;set;}
        //public DbSet<Content> Content {get;set;}
        //public DbSet<ActionLog> Logs {get;set;}

        //public IQueryable<T> GetAll<T>(params long[] ids) where T : GenericModel 
        //{
        //    var set = Set<T>();
        //    IQueryable<T> result = set;

        //    if(ids != null && ids.Length > 0)
        //        result = set.Where(x => ids.Contains(x.id));

        //    //Remove deleted stuff
        //    result = result.Where(x => (x.status & (int)ModelStatus.Deleted) == 0);

        //    return result;
        //}

        //public async Task<T> GetSingleAsync<T>(long id) where T : GenericModel
        //{
        //    return await GetAll<T>(id).FirstOrDefaultAsync();
        //}
    }
}