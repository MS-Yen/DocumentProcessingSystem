using DocumentService.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace DocumentService.Infrastructure.Data;

/// <summary>
/// EF Core database context for PostgreSQL.
/// Configures how the Document entity maps to the database table.
/// </summary>
public class DocumentDbContext : DbContext
{
    public DocumentDbContext(DbContextOptions<DocumentDbContext> options)
        : base(options)
    {
    }

    public DbSet<Document> Documents => Set<Document>();

    /// <summary>
    /// Configure the database schema using the Fluent API.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Document>(entity =>
        {
            entity.ToTable("documents");

            entity.HasKey(d => d.Id);

            entity.Property(d => d.FileName)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(d => d.ContentType)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(d => d.MongoFileId)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(d => d.RagError)
                .HasMaxLength(2000);

            // Store the enum as a string in the database for readability
            entity.Property(d => d.Status)
                .HasConversion<string>()
                .HasMaxLength(20);

            entity.HasIndex(d => d.UploadedAt);
        });
    }
}
