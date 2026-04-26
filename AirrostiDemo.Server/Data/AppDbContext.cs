using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AirrostiDemo.Server.Data
{
    public class AppDbContext : IdentityDbContext<AppUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<SavedReport> SavedReports => Set<SavedReport>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<SavedReport>(e =>
            {
                e.HasIndex(r => r.UserId);
                e.Property(r => r.DrugName).HasMaxLength(200).IsRequired();
                e.Property(r => r.UserId).IsRequired();
                e.Property(r => r.ReportJson).IsRequired();
            });
        }
    }
}
