using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.EntityFrameworkCore;
namespace ITMOParser
{
    public class ApplicationContext : DbContext
    {
        public DbSet<Application> itmo { get; set; }
        public ApplicationContext() { }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseNpgsql(Program.dbConnectionString);
        }
        

    }
    public class Application
    {
        public int id { get; set; }
        public int applicationId   { get; set; }
        public int points { get; set; }
        public int priority { get; set; }
        public bool confirmation { get; set; }
        public string profile  { get; set; }
    }
}
