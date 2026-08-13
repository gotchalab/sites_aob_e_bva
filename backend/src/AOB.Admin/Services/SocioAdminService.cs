using AOB.Core.Entities;
using AOB.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AOB.Admin.Services;

public class SocioAdminService(
    AppDbContext db,
    UserManager<ApplicationUser> users)
{
    public Task<List<Socio>> ListAsync(int siteId, string? search, int page, int pageSize) =>
        Query(siteId, search)
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();

    public Task<int> CountAsync(int siteId, string? search) => Query(siteId, search).CountAsync();

    private IQueryable<Socio> Query(int siteId, string? search)
    {
        var q = db.Socios.AsNoTracking().Where(s => s.SiteId == siteId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = $"%{search.Trim()}%";
            q = q.Where(x => EF.Functions.ILike(x.NomeCompleto, s) ||
                             EF.Functions.ILike(x.Email, s) ||
                             EF.Functions.ILike(x.NumeroSocio, s));
        }
        return q;
    }

    public Task<Socio?> GetAsync(int id) =>
        db.Socios.Include(s => s.Quotas).Include(s => s.Pedidos)
            .FirstOrDefaultAsync(s => s.Id == id);

    public async Task SaveAsync(Socio socio)
    {
        socio.UpdatedAt = DateTime.UtcNow;
        if (socio.Id == 0)
        {
            socio.CreatedAt = DateTime.UtcNow;
            db.Socios.Add(socio);
        }
        else
        {
            db.Socios.Update(socio);
        }
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var s = await db.Socios.FindAsync(id);
        if (s is null) return;
        db.Socios.Remove(s);
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Cria user Identity ligado ao socio + adiciona role Socio. Devolve mensagem.
    /// </summary>
    public async Task<(bool Ok, string Message)> CreateUserForSocioAsync(int socioId, string initialPassword)
    {
        var socio = await db.Socios.FindAsync(socioId);
        if (socio is null) return (false, "Socio nao encontrado.");
        if (socio.UserId is not null) return (false, "Socio ja tem user associado.");

        var existing = await users.FindByEmailAsync(socio.Email);
        if (existing is not null) return (false, $"Ja existe um user com o email {socio.Email}.");

        var user = new ApplicationUser
        {
            UserName = socio.Email,
            Email = socio.Email,
            EmailConfirmed = true,
            FullName = socio.NomeCompleto,
            SocioId = socio.Id,
        };
        var res = await users.CreateAsync(user, initialPassword);
        if (!res.Succeeded)
            return (false, string.Join("; ", res.Errors.Select(e => e.Description)));

        await users.AddToRoleAsync(user, AppRoles.Socio);
        socio.UserId = user.Id;
        socio.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return (true, "User criado — envia a password inicial ao sócio.");
    }

    public Task<List<PedidoAnilha>> ListPedidosPendentesAsync(int siteId) =>
        db.PedidosAnilha.AsNoTracking()
            .Include(p => p.Socio)
            .Where(p => p.Socio.SiteId == siteId && p.Estado == PedidoAnilhaEstado.Pendente)
            .OrderBy(p => p.DataPedido)
            .ToListAsync();

    public Task<List<PedidoAnilha>> ListPedidosAsync(int siteId) =>
        db.PedidosAnilha.AsNoTracking()
            .Include(p => p.Socio)
            .Where(p => p.Socio.SiteId == siteId)
            .OrderByDescending(p => p.DataPedido)
            .Take(200)
            .ToListAsync();

    public Task<List<Quota>> ListQuotasAsync(int socioId) =>
        db.Quotas.AsNoTracking()
            .Where(q => q.SocioId == socioId)
            .OrderByDescending(q => q.Ano)
            .ToListAsync();

    public async Task SaveQuotaAsync(Quota quota)
    {
        if (quota.Id == 0) db.Quotas.Add(quota);
        else db.Quotas.Update(quota);
        await db.SaveChangesAsync();
    }

    public async Task DeleteQuotaAsync(int id)
    {
        var q = await db.Quotas.FindAsync(id);
        if (q is null) return;
        db.Quotas.Remove(q);
        await db.SaveChangesAsync();
    }

    public async Task UpdatePedidoEstadoAsync(int id, PedidoAnilhaEstado estado, string? notas)
    {
        var p = await db.PedidosAnilha.FindAsync(id);
        if (p is null) return;
        p.Estado = estado;
        if (notas is not null) p.Notas = notas;
        if (estado == PedidoAnilhaEstado.Entregue) p.DataEntrega ??= DateTime.UtcNow;
        p.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }
}
