using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AirrostiDemo.Server.Data
{
    /// <summary>
    /// The single EF Core <see cref="DbContext"/> for the application. It
    /// extends <see cref="IdentityDbContext{TUser}"/> so that all of the
    /// AspNet Identity tables (users, claims, logins, etc.) are managed
    /// alongside the demo's own <see cref="SavedReport"/> table in one
    /// SQLite file.
    /// </summary>
    /// <remarks>
    /// The connection string lives at <c>ConnectionStrings:DefaultConnection</c>
    /// in <c>appsettings*.json</c> — typically <c>Data Source=demo.db</c>, a
    /// SQLite file that ships next to the server binary. EF Core migrations
    /// are applied automatically at startup (see <c>Program.cs</c>), so a
    /// fresh checkout boots with an empty schema and no manual <c>dotnet ef
    /// database update</c> step.
    /// </remarks>
    public class AppDbContext : IdentityDbContext<AppUser>
    {
        /// <summary>
        /// Standard EF Core constructor: receives the configured
        /// <see cref="DbContextOptions{AppDbContext}"/> from the DI container
        /// and forwards them to the base <see cref="IdentityDbContext{TUser}"/>
        /// implementation.
        /// </summary>
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        /// <summary>
        /// The set of saved drug-search snapshots, keyed off
        /// <see cref="SavedReport.UserId"/>. Using <c>Set&lt;T&gt;()</c>
        /// rather than the property-initializer pattern makes the get-only
        /// property work without nullable warnings.
        /// </summary>
        public DbSet<SavedReport> SavedReports => Set<SavedReport>();

        /// <summary>
        /// Fluent-API model configuration for the SavedReports table.
        /// </summary>
        /// <remarks>
        /// We always call <c>base.OnModelCreating(builder)</c> first — the
        /// <see cref="IdentityDbContext{TUser}"/> base method wires up every
        /// AspNet Identity table, and skipping it would silently break login.
        /// </remarks>
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Configure SavedReport: index UserId for fast per-user lookup,
            // require the JSON payload, and cap DrugName so we don't accept
            // unbounded user-controlled strings.
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
