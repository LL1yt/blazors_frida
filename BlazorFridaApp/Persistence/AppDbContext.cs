using Microsoft.EntityFrameworkCore;
using BlazorFridaApp.MemoryScanner.Models;

namespace BlazorFridaApp.Persistence
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<ApplicationSetting> ApplicationSettings { get; set; }
        public DbSet<LockedAddress> LockedAddresses { get; set; }
        public DbSet<ScanProfile> ScanProfiles { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ApplicationSetting>(e => {
                e.Property(a => a.Key).HasMaxLength(256);
                e.HasIndex(a => a.Key).IsUnique();
            });
            
            modelBuilder.Entity<LockedAddress>(e => {
                e.Property(l => l.ProcessName).HasMaxLength(256);
                e.HasIndex(l => new { l.ProcessName, l.Address });
            });

            modelBuilder.Entity<ScanProfile>(e => {
                e.Property(s => s.Name).HasMaxLength(256);
                e.Property(s => s.ProcessName).HasMaxLength(256);
                e.HasIndex(s => s.Name).IsUnique();
            });
        }

        public override async Task<int> SaveChangesAsync(
            CancellationToken cancellationToken = new())
        {
            foreach (var entry in ChangeTracker.Entries<ApplicationSetting>())
            {
                if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
                {
                    entry.Entity.LastModified = DateTime.UtcNow;
                }
            }
            return await base.SaveChangesAsync(cancellationToken);
        }
    }
}