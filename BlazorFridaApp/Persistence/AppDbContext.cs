using Microsoft.EntityFrameworkCore;
using BlazorFridaApp.MemoryScanner.Models;

namespace BlazorFridaApp.Persistence
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<ApplicationSetting> ApplicationSettings { get; set; } = null!;
        public DbSet<LockedAddress> LockedAddresses { get; set; } = null!;
        public DbSet<ProcessSettings> ProcessSettings { get; set; } = null!;
        public DbSet<ScanProfile> ScanProfiles { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            // Enable SQLite foreign key support
            modelBuilder.HasAnnotation("Sqlite:Pragma", "foreign_keys=ON");

            modelBuilder.Entity<ApplicationSetting>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Key).IsUnique();
                entity.Property(e => e.Key).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Value).IsRequired();
            });

            modelBuilder.Entity<ProcessSettings>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.ProcessName).IsUnique();
                entity.Property(e => e.ProcessName).IsRequired().HasMaxLength(255);
                entity.Property(e => e.Notes).HasMaxLength(1000);

                // Configure relationships
                entity.HasMany(e => e.LockedAddresses)
                    .WithOne(e => e.ProcessSettings)
                    .HasForeignKey(e => e.ProcessSettingsId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(e => e.ScanProfiles)
                    .WithOne(e => e.ProcessSettings)
                    .HasForeignKey(e => e.ProcessSettingsId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<LockedAddress>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.ProcessSettingsId, e.Address }).IsUnique();
                entity.Property(e => e.ProcessName).IsRequired().HasMaxLength(255);
                entity.Property(e => e.ValueType).IsRequired().HasMaxLength(50);
                entity.Property(e => e.OriginalBytes).IsRequired();
                entity.Property(e => e.CurrentValue).IsRequired();
            });

            modelBuilder.Entity<ScanProfile>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
                entity.Property(e => e.ProcessName).IsRequired().HasMaxLength(255);
                entity.Property(e => e.Pattern).IsRequired();
                entity.Property(e => e.Mask).IsRequired().HasMaxLength(255);
                entity.Property(e => e.Offsets).IsRequired();

                // Create a compound index on ProcessName and Name
                entity.HasIndex(e => new { e.ProcessSettingsId, e.Name }).IsUnique();
            });
        }
    }
}