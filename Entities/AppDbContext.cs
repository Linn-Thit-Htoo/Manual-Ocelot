using Microsoft.EntityFrameworkCore;

namespace Manual_Ocelot.Entities
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions options) : base(options)
        {
        }

        public DbSet<Tbl_ServiceRegistry> Tbl_ServiceRegistries { get; set; }
    }
}
