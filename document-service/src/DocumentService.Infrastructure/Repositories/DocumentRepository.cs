using DocumentService.Core.Interfaces;
using DocumentService.Core.Models;
using DocumentService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DocumentService.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of IDocumentRepository for PostgreSQL.
/// </summary>
public class DocumentRepository : IDocumentRepository
{
    private readonly DocumentDbContext _context;

    public DocumentRepository(DocumentDbContext context)
    {
        _context = context;
    }

    public async Task<Document?> GetByIdAsync(Guid id)
    {
        return await _context.Documents.FindAsync(id);
    }

    public async Task<IReadOnlyList<Document>> GetAllAsync()
    {
        return await _context.Documents
            .AsNoTracking()
            .OrderByDescending(d => d.UploadedAt)
            .ToListAsync();
    }

    public async Task AddAsync(Document document)
    {
        _context.Documents.Add(document);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Document document)
    {
        _context.Documents.Update(document);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var document = await _context.Documents.FindAsync(id);
        if (document is null)
            return false;

        _context.Documents.Remove(document);
        await _context.SaveChangesAsync();
        return true;
    }
}
