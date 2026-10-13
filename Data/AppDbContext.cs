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

        public DbSet<Employee> Employees { get; set; } = null!;
        public DbSet<Client> Clients { get; set; } = null!;
        public DbSet<Site> Sites { get; set; } = null!;
        public DbSet<BillingRule> BillingRules { get; set; } = null!;
        public DbSet<ServiceScheduleRule> ServiceScheduleRules { get; set; } = null!;
        public DbSet<NhnBusiness> Businesses { get; set; } = null!;
        public DbSet<NhnReceipt> Receipts { get; set; } = null!;
        public DbSet<NhnLedgerRule> LedgerRules { get; set; } = null!;
        public DbSet<NhnLedgerQuarter> LedgerQuarters { get; set; } = null!;
        public DbSet<NhnLedgerTransaction> LedgerTransactions { get; set; } = null!;
        public DbSet<TodoGroup> TodoGroups { get; set; } = null!;
        public DbSet<TodoItem> TodoItems { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
        }
    }
}
