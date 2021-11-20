using Microsoft.EntityFrameworkCore;

namespace contentapi.Db
{
    public class ContentApiDbContext : DbContext
    {
        public DbSet<User> Users => Set<User>();
        public DbSet<Ban> Bans => Set<Ban>();

        public ContentApiDbContext(DbContextOptions<ContentApiDbContext> options) : base(options)
        {
            //By default, we don't want to track the things. We can manually call update
            //on the objects we want to save.
            this.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        }
    }
}