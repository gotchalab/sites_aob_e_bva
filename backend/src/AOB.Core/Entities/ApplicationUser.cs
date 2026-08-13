using Microsoft.AspNetCore.Identity;

namespace AOB.Core.Entities;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    public int? PreferredSiteId { get; set; }
    public Site? PreferredSite { get; set; }

    public int? SocioId { get; set; }
}

public static class AppRoles
{
    public const string Admin = "Admin";
    public const string Editor = "Editor";
    public const string Socio = "Socio";
}
