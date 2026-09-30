using Microsoft.EntityFrameworkCore;
using NHN.Power.API.Models;

namespace NHN.Power.API.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<Client> Clients { get; set; } = null!;
        public DbSet<Site> Sites { get; set; } = null!;
        public DbSet<BillingRule> BillingRules { get; set; } = null!;
        public DbSet<ServiceScheduleRule> ServiceScheduleRules { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
        }
    }
}
