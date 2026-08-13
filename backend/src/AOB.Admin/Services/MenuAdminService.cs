using AOB.Core.Entities;
using AOB.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AOB.Admin.Services;

public class MenuAdminService(AppDbContext db)
{
    public Task<List<MenuItem>> ListAsync(int siteId, string? menuType = null)
    {
        var q = db.MenuItems.AsNoTracking()
            .Where(m => m.SiteId == siteId);
        if (!string.IsNullOrWhiteSpace(menuType))
            q = q.Where(m => m.MenuType == menuType);
        return q.OrderBy(m => m.MenuType).ThenBy(m => m.SortOrder).ToListAsync();
    }

    public Task<List<string>> MenuTypesAsync(int siteId) =>
        db.MenuItems.AsNoTracking()
            .Where(m => m.SiteId == siteId)
            .Select(m => m.MenuType)
            .Distinct()
            .OrderBy(t => t)
            .ToListAsync();

    public Task<MenuItem?> GetAsync(int id) => db.MenuItems.FirstOrDefaultAsync(m => m.Id == id);

    public async Task SaveAsync(MenuItem item)
    {
        if (item.Id == 0) db.MenuItems.Add(item);
        else db.MenuItems.Update(item);
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var m = await db.MenuItems.FindAsync(id);
        if (m is null) return;
        db.MenuItems.Remove(m);
        await db.SaveChangesAsync();
    }

    public async Task MoveAsync(int id, int delta)
    {
        var item = await db.MenuItems.FindAsync(id);
        if (item is null) return;
        item.SortOrder += delta;
        await db.SaveChangesAsync();
    }
}
