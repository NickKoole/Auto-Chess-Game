using Microsoft.EntityFrameworkCore;

namespace Api.Persistence;

public sealed class GameDbContext(DbContextOptions<GameDbContext> options) : DbContext(options)
{
    public DbSet<PlayerSave> PlayerSaves => Set<PlayerSave>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<PlayerSave>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PlayerName).HasMaxLength(64).IsRequired();
            entity.Property(e => e.StateJson).IsRequired();
            entity.HasIndex(e => e.PlayerName).IsUnique();
        });
    }
}
