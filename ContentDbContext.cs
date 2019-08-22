using System.Collections.Generic;
using contentapi.Models;
using Microsoft.EntityFrameworkCore;

namespace contentapi
{
    //Every single "MyContext" will essentially look the same: you just add 
    //the available fields you can search.
    public class ContentDbContext : DbContext
    {
        public ContentDbContext(DbContextOptions<ContentDbContext> options) : base(options) { }

        //public DbSet<Message> Messages { get; set; }
        public DbSet<User> Users {get; set;}
        //public DbSet<Room> Rooms {get; set;}
    }
}