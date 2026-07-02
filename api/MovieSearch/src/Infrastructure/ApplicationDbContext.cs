using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure;

public class ApplicationDbContext : DbContext
{
    public DbSet<Movie> Movies => Set<Movie>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.Entity<Movie>(entity =>
        {
            entity.Property(p => p.Embedding)
                  .HasColumnType("vector(768)");

            entity.HasIndex(p => p.Embedding)
                  .HasMethod("hnsw")
                  .HasOperators("vector_cosine_ops")
                  .HasStorageParameter("m", 16)
                  .HasStorageParameter("ef_construction", 64);
        });
    }
}
