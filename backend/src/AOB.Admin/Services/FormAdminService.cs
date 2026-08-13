using AOB.Core.Entities;
using AOB.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AOB.Admin.Services;

public class FormAdminService(AppDbContext db)
{
    public Task<List<FormSubmission>> ListAsync(int siteId, FormStatus? status, FormType? type, int page, int pageSize) =>
        BuildQuery(siteId, status, type)
            .OrderByDescending(f => f.SubmittedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();

    public Task<int> CountAsync(int siteId, FormStatus? status, FormType? type) =>
        BuildQuery(siteId, status, type).CountAsync();

    private IQueryable<FormSubmission> BuildQuery(int siteId, FormStatus? status, FormType? type)
    {
        var q = db.FormSubmissions.AsNoTracking()
            .Where(f => f.SiteId == siteId);
        if (status.HasValue) q = q.Where(f => f.Status == status.Value);
        if (type.HasValue) q = q.Where(f => f.FormType == type.Value);
        return q;
    }

    public Task<FormSubmission?> GetAsync(int id) =>
        db.FormSubmissions.FirstOrDefaultAsync(f => f.Id == id);

    public async Task SetStatusAsync(int id, FormStatus status, string? handledById, string? notes)
    {
        var f = await db.FormSubmissions.FindAsync(id);
        if (f is null) return;
        f.Status = status;
        f.HandledById = handledById;
        f.HandledAt = DateTime.UtcNow;
        if (notes is not null) f.Notes = notes;
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var f = await db.FormSubmissions.FindAsync(id);
        if (f is null) return;
        db.FormSubmissions.Remove(f);
        await db.SaveChangesAsync();
    }
}
