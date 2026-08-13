using AOB.Core.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AOB.Admin.Services;

public record UserRow(string Id, string Email, string FullName, DateTime CreatedAt,
    DateTime? LastLoginAt, bool LockedOut, IReadOnlyList<string> Roles);

public class UserAdminService(
    UserManager<ApplicationUser> users,
    RoleManager<IdentityRole> roles)
{
    public async Task<List<UserRow>> ListAsync()
    {
        var all = await users.Users.AsNoTracking().OrderBy(u => u.Email).ToListAsync();
        var rows = new List<UserRow>(all.Count);
        foreach (var u in all)
        {
            var userRoles = await users.GetRolesAsync(u);
            rows.Add(new UserRow(
                u.Id, u.Email ?? "", u.FullName, u.CreatedAt, u.LastLoginAt,
                await users.IsLockedOutAsync(u), userRoles.ToList()));
        }
        return rows;
    }

    public async Task<List<string>> AllRoles() =>
        await roles.Roles.AsNoTracking().Select(r => r.Name!).OrderBy(n => n).ToListAsync();

    public async Task<ApplicationUser?> Get(string id) => await users.FindByIdAsync(id);

    public async Task<(bool Ok, string Message)> CreateAsync(
        string email, string fullName, string password, IEnumerable<string> selectedRoles)
    {
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = fullName,
            CreatedAt = DateTime.UtcNow,
        };
        var res = await users.CreateAsync(user, password);
        if (!res.Succeeded)
            return (false, string.Join("; ", res.Errors.Select(e => e.Description)));

        foreach (var role in selectedRoles)
        {
            if (await roles.RoleExistsAsync(role))
                await users.AddToRoleAsync(user, role);
        }
        return (true, "User criado.");
    }

    public async Task<(bool Ok, string Message)> SetRolesAsync(
        string userId, IEnumerable<string> selectedRoles)
    {
        var u = await users.FindByIdAsync(userId);
        if (u is null) return (false, "User nao encontrado");
        var current = await users.GetRolesAsync(u);
        var target = selectedRoles.ToHashSet();

        var toAdd = target.Except(current).ToList();
        var toRemove = current.Except(target).ToList();

        if (toRemove.Count > 0)
        {
            var r1 = await users.RemoveFromRolesAsync(u, toRemove);
            if (!r1.Succeeded)
                return (false, string.Join("; ", r1.Errors.Select(e => e.Description)));
        }
        if (toAdd.Count > 0)
        {
            var r2 = await users.AddToRolesAsync(u, toAdd);
            if (!r2.Succeeded)
                return (false, string.Join("; ", r2.Errors.Select(e => e.Description)));
        }
        return (true, "Roles atualizadas.");
    }

    public async Task<(bool Ok, string Message)> ResetPasswordAsync(string userId, string newPassword)
    {
        var u = await users.FindByIdAsync(userId);
        if (u is null) return (false, "User nao encontrado");
        var token = await users.GeneratePasswordResetTokenAsync(u);
        var res = await users.ResetPasswordAsync(u, token, newPassword);
        return res.Succeeded
            ? (true, "Password atualizada.")
            : (false, string.Join("; ", res.Errors.Select(e => e.Description)));
    }

    public async Task<(bool Ok, string Message)> UnlockAsync(string userId)
    {
        var u = await users.FindByIdAsync(userId);
        if (u is null) return (false, "User nao encontrado");
        await users.SetLockoutEndDateAsync(u, null);
        return (true, "Desbloqueado.");
    }

    public async Task<(bool Ok, string Message)> DeleteAsync(string userId)
    {
        var u = await users.FindByIdAsync(userId);
        if (u is null) return (false, "User nao encontrado");
        var res = await users.DeleteAsync(u);
        return res.Succeeded
            ? (true, "User eliminado.")
            : (false, string.Join("; ", res.Errors.Select(e => e.Description)));
    }
}
