using Microsoft.EntityFrameworkCore;

namespace ITMOParser
{
    public class ApplicationContext : DbContext
    {
        public DbSet<Application> itmo { get; set; }
        public ApplicationContext() { }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseNpgsql(Program.DBConnectionString);
        }
    }

    public class Application
    {
        public long Id { get; set; }
        public int AbiturientId { get; set; }
        public int PointsFinal { get; set; }
        public int PointsBase { get; set; }
        public List<int> Exams { get; set; }
        public int IndividualAchievements { get; set; }
        public int Priority { get; set; }
        public bool Advantage { get; set; }
        public bool Confirmation { get; set; }
        public string Profile  { get; set; }
        public bool? Passes { get; set; } 
    }
}
