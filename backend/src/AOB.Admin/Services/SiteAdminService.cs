using AOB.Core.Entities;
using AOB.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AOB.Admin.Services;

/// <summary>
/// CRUD helpers para configuração do site (Tagline, HomeConfig, Announcement).
/// </summary>
public class SiteAdminService(AppDbContext db)
{
    public Task<List<Site>> ListAsync() =>
        db.Sites.AsNoTracking().OrderBy(s => s.Slug).ToListAsync();

    public Task<Site?> GetAsync(int id) =>
        db.Sites.FirstOrDefaultAsync(s => s.Id == id);

    public async Task SaveAsync(Site s)
    {
        db.Sites.Update(s);
        await db.SaveChangesAsync();
    }
}
