using AOB.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AOB.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Missing connection string 'Default'");

        services.AddDbContext<AppDbContext>(opt =>
            opt.UseNpgsql(connectionString, npg =>
                npg.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));

        return services;
    }

    public static Action<Microsoft.AspNetCore.Identity.IdentityOptions> ConfigureIdentity => o =>
    {
        o.Password.RequiredLength = 12;
        o.Password.RequireDigit = true;
        o.Password.RequireLowercase = true;
        o.Password.RequireUppercase = true;
        o.Password.RequireNonAlphanumeric = true;
        o.User.RequireUniqueEmail = true;
        o.SignIn.RequireConfirmedEmail = false;
        o.Lockout.MaxFailedAccessAttempts = 5;
        o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    };
}
