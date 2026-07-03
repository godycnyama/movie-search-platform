using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations;

/// <summary>
/// Maps <see cref="Movie"/> onto the pipeline-owned <c>movies</c> table (README §6/§9).
/// Column names come from the snake_case naming convention configured on the context;
/// only pgvector-specific pieces need explicit configuration here.
/// </summary>
public sealed class MovieConfiguration : IEntityTypeConfiguration<Movie>
{
    public void Configure(EntityTypeBuilder<Movie> builder)
    {
        builder.ToTable("movies");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Embedding)
               .HasColumnType("vector(768)");

        builder.HasIndex(m => m.Embedding)
               .HasMethod("hnsw")
               .HasOperators("vector_cosine_ops")
               .HasStorageParameter("m", 16)
               .HasStorageParameter("ef_construction", 64);
    }
}
