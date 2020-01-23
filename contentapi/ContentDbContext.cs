using contentapi.Models;
using Microsoft.EntityFrameworkCore;

//DbContext should be kept a simple place to "declare" sets. Other services
//will do the complicated things surrounding each model.
namespace contentapi
{
    //Every single "MyContext" will essentially look the same: you just add 
    //the available fields you can search.
    public class ContentDbContext : DbContext
    {
        public ContentDbContext(DbContextOptions<ContentDbContext> options) : base(options) { }

        public DbSet<UserEntity> UserEntities {get;set;}
        public DbSet<CategoryEntity> CategoryEntities {get;set;}
        public DbSet<ContentEntity> ContentEntities {get;set;}
        public DbSet<CommentEntity> SubcontentEntities {get;set;}
        public DbSet<Entity> Entities {get;set;}
        public DbSet<EntityAccess> EntityAccess {get;set;}
        public DbSet<EntityLog> EntityLogs {get;set;}
    }
}
