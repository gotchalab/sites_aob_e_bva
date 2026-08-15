using System.Text;
using System.Threading.RateLimiting;
using AOB.Api.Auth;
using AOB.Api.Endpoints;
using AOB.Application.Content;
using AOB.Application.Forms;
using AOB.Core.Entities;
using AOB.Infrastructure;
using AOB.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/api-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14)
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddScoped<ShortcodeExpander>();

    // Turnstile: em prod o secret e obrigatorio (bypass do verifier so e aceitavel em dev)
    if (!builder.Environment.IsDevelopment()
        && string.IsNullOrWhiteSpace(builder.Configuration["Turnstile:SecretKey"]))
    {
        throw new InvalidOperationException("Turnstile:SecretKey obrigatorio em producao");
    }
    builder.Services.AddScoped<TurnstileVerifier>();
    builder.Services.AddScoped<EmailSender>();
    builder.Services.AddHttpClient();
    builder.Services.AddMemoryCache();
    builder.Services.AddOpenApi();

    // Identity (users + roles) — sem UI, apenas para o UserManager/SignInManager
    builder.Services.AddIdentityCore<ApplicationUser>(DependencyInjection.ConfigureIdentity)
        .AddRoles<IdentityRole>()
        .AddEntityFrameworkStores<AppDbContext>()
        .AddSignInManager()
        .AddDefaultTokenProviders();

    // JWT — resolve signing key com fallback dev, depois grava back nos Options
    var jwtOpts = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
    if (string.IsNullOrEmpty(jwtOpts.SigningKey))
    {
        jwtOpts.SigningKey = builder.Environment.IsDevelopment()
            ? "dev-secret-key-please-change-in-prod-min-32chars-long!!"
            : throw new InvalidOperationException("Jwt:SigningKey obrigatorio em producao");
    }
    builder.Services.Configure<JwtOptions>(o =>
    {
        o.Issuer = jwtOpts.Issuer;
        o.Audience = jwtOpts.Audience;
        o.SigningKey = jwtOpts.SigningKey;
        o.AccessTokenMinutes = jwtOpts.AccessTokenMinutes;
        o.RefreshTokenDays = jwtOpts.RefreshTokenDays;
    });
    builder.Services.AddScoped<JwtService>();
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(o =>
        {
            o.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtOpts.Issuer,
                ValidAudience = jwtOpts.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOpts.SigningKey)),
                ClockSkew = TimeSpan.FromSeconds(30),
            };
        });
    builder.Services.AddAuthorization(o =>
    {
        o.AddPolicy("Socio", p => p.RequireAuthenticatedUser().RequireRole(AppRoles.Socio, AppRoles.Admin));
        o.AddPolicy("Admin", p => p.RequireAuthenticatedUser().RequireRole(AppRoles.Admin));
    });

    builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
        .WithOrigins(
            builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
            ?? new[] { "http://localhost:3000", "http://localhost:3001" })
        .AllowAnyHeader()
        .AllowAnyMethod()));

    builder.Services.AddRateLimiter(opt =>
    {
        opt.RejectionStatusCode = 429;
        opt.AddPolicy("public", ctx => RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ctx.Connection.RemoteIpAddress?.ToString() ?? "anon",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
        opt.AddPolicy("forms", ctx => RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ctx.Connection.RemoteIpAddress?.ToString() ?? "anon",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(10),
                QueueLimit = 0,
            }));
    });

    var app = builder.Build();

    app.UseSerilogRequestLogging();

    app.Use(async (ctx, next) =>
    {
        ctx.Response.Headers.Append("X-Content-Type-Options", "nosniff");
        ctx.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
        ctx.Response.Headers.Append("X-Frame-Options", "SAMEORIGIN");
        await next();
    });

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }

    app.UseCors();
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();

    var uploadsRoot = builder.Configuration["Uploads:RootPath"];
    if (!string.IsNullOrWhiteSpace(uploadsRoot) && Directory.Exists(uploadsRoot))
    {
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(Path.GetFullPath(uploadsRoot)),
            RequestPath = "/uploads",
            ServeUnknownFileTypes = false,
            OnPrepareResponse = ctx =>
            {
                // Alguns uploads legados têm extensão .jpg mas conteúdo PNG (ou vice-versa).
                // Com nosniff activo, o browser recusa-se a renderizar. Corrigimos o Content-Type
                // olhando para os magic bytes.
                var detected = DetectImageMime(ctx.File);
                if (detected != null)
                    ctx.Context.Response.ContentType = detected;
                ctx.Context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
                ctx.Context.Response.Headers.Append("Cache-Control", "public, max-age=3600");

                // PDFs abrem inline no browser; todos os outros ficheiros (docx, xlsx, etc.)
                // forçam download com o nome original do ficheiro.
                var mime = ctx.Context.Response.ContentType ?? "";
                if (mime.StartsWith("application/pdf", StringComparison.OrdinalIgnoreCase)
                    || mime.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                {
                    ctx.Context.Response.Headers.Append("Content-Disposition", "inline");
                }
                else
                {
                    var fileName = Uri.UnescapeDataString(ctx.File.Name);
                    ctx.Context.Response.Headers.Append(
                        "Content-Disposition",
                        $"attachment; filename=\"{fileName}\"");
                }
            },
        });
    }

    app.MapPublic().RequireRateLimiting("public");
    app.MapForms().RequireRateLimiting("forms");
    app.MapConvoyage();
    app.MapNomenclature();
    app.MapAuth();
    app.MapMe();
    app.MapRevalidate();
    app.MapGet("/", () => "AOB API. Endpoints: /api/sites/{aob|bva}/{articles|downloads|categories|menu|forms} · /api/auth · /api/me");
    app.MapGet("/health", () => Results.Ok(new { status = "ok", version = "1.0" }));

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminado inesperadamente");
}
finally
{
    Log.CloseAndFlush();
}

static string? DetectImageMime(Microsoft.Extensions.FileProviders.IFileInfo file)
{
    try
    {
        using var s = file.CreateReadStream();
        Span<byte> buf = stackalloc byte[12];
        var n = s.Read(buf);
        if (n >= 8 && buf[0] == 0x89 && buf[1] == 0x50 && buf[2] == 0x4E && buf[3] == 0x47) return "image/png";
        if (n >= 3 && buf[0] == 0xFF && buf[1] == 0xD8 && buf[2] == 0xFF) return "image/jpeg";
        if (n >= 6 && buf[0] == 0x47 && buf[1] == 0x49 && buf[2] == 0x46 && buf[3] == 0x38) return "image/gif";
        if (n >= 12 && buf[0] == 0x52 && buf[1] == 0x49 && buf[2] == 0x46 && buf[3] == 0x46
            && buf[8] == 0x57 && buf[9] == 0x45 && buf[10] == 0x42 && buf[11] == 0x50) return "image/webp";
    }
    catch { }
    return null;
}
