using Botijas.Domain.Entities;
using Botijas.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Botijas.Infrastructure.Data;

public class BotijasDbContext : DbContext
{
    public BotijasDbContext(DbContextOptions<BotijasDbContext> options) : base(options)
    {
    }

    public DbSet<Customer> Customers { get; set; }
    public DbSet<RefillOrder> Orders { get; set; }
    public DbSet<Cylinder> Cylinders { get; set; }
    public DbSet<CylinderRef> CylinderRefs { get; set; }
    public DbSet<PrintJob> PrintJobs { get; set; }
    public DbSet<CylinderHistoryEntry> CylinderHistory { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(e => e.CustomerId);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Phone).HasConversion(
                v => v.Value,
                v => PhoneNumber.Create(v)
            ).IsRequired().HasMaxLength(20);
            entity.Property(e => e.CreatedAt).IsRequired();

            entity.HasIndex(e => e.Phone).IsUnique();
        });

        modelBuilder.Entity<RefillOrder>(entity =>
        {
            entity.HasKey(e => e.OrderId);
            entity.Property(e => e.CustomerId).IsRequired();
            entity.Property(e => e.Status).IsRequired().HasConversion<string>();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.HasMany(e => e.Cylinders)
                  .WithOne()
                  .HasForeignKey(cr => cr.OrderId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Cylinder>(entity =>
        {
            entity.HasKey(e => e.CylinderId);
            entity.Property(e => e.LabelToken).HasConversion(
                v => v != null ? v.Value : null,
                v => v != null ? LabelToken.Create(v) : null
            ).HasMaxLength(100);
            entity.Property(e => e.State).IsRequired().HasConversion<string>();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.HasIndex(e => e.LabelToken).IsUnique();
        });

        modelBuilder.Entity<CylinderRef>(entity =>
        {
            entity.HasKey(e => new { e.OrderId, e.CylinderId });
            entity.Property(e => e.State).IsRequired().HasConversion<string>();
        });

        modelBuilder.Entity<PrintJob>(entity =>
        {
            entity.HasKey(e => e.PrintJobId);
            entity.Property(e => e.StoreId).IsRequired();
            entity.Property(e => e.Quantity).IsRequired();
            entity.Property(e => e.TemplateId).HasMaxLength(100);
            entity.Property(e => e.Status).IsRequired().HasConversion<string>();
            entity.Property(e => e.ErrorMessage).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).IsRequired();
        });

        modelBuilder.Entity<CylinderHistoryEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CylinderId).IsRequired();
            entity.Property(e => e.EventType).IsRequired().HasConversion<string>();
            entity.Property(e => e.Details).HasMaxLength(500);
            entity.Property(e => e.Timestamp).IsRequired();
            entity.HasIndex(e => e.CylinderId);
        });
    }
}
