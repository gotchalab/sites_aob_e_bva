using AOB.Admin.Components;
using AOB.Admin.Services;
using AOB.Application.Forms;
using AOB.Core.Entities;
using AOB.Infrastructure;
using AOB.Infrastructure.Persistence;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(DependencyInjection.ConfigureIdentity)
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(o =>
{
    o.LoginPath = "/login";
    o.LogoutPath = "/logout";
    o.AccessDeniedPath = "/access-denied";
    o.ExpireTimeSpan = TimeSpan.FromHours(8);
    o.SlidingExpiration = true;
    o.Cookie.Name = "aob.admin";
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Lax;
});

builder.Services.AddAuthorization(o =>
{
    o.AddPolicy(AppRoles.Admin, p => p.RequireRole(AppRoles.Admin));
    o.AddPolicy(AppRoles.Editor, p => p.RequireRole(AppRoles.Admin, AppRoles.Editor));
});

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AuthenticationStateProvider, RevalidatingIdentityAuthenticationStateProvider<ApplicationUser>>();

builder.Services.AddScoped<ContentService>();
builder.Services.AddScoped<ClamAvScanner>();
builder.Services.AddScoped<UploadService>();
builder.Services.AddScoped<UserAdminService>();
builder.Services.AddScoped<MenuAdminService>();
builder.Services.AddScoped<FormAdminService>();
builder.Services.AddScoped<SocioAdminService>();
builder.Services.AddScoped<SponsorAdminService>();
builder.Services.AddScoped<SiteAdminService>();
builder.Services.AddScoped<ConvoyageAdminService>();
builder.Services.AddScoped<NomenclatureAdminService>();
builder.Services.AddScoped<TransportPlanAdminService>();
builder.Services.AddScoped<EmailSender>();
// Timeout definido AQUI porque HttpClient.Timeout so pode ser configurado antes
// da primeira request; dentro do NotifyAsync lancava InvalidOperationException
// apos a 1a chamada — as 3 revalidacoes por save (/, /artigos, /artigos/{slug})
// so a 1a passava. Configuravel via Revalidate:TimeoutSeconds (default 10s —
// chamada interna loopback ao Next.js; 3s era demasiado apertado se o Next
// estivesse a re-renderizar uma pagina pesada em SSR).
builder.Services.AddHttpClient<RevalidateNotifier>((sp, c) =>
{
    var seconds = sp.GetRequiredService<IConfiguration>().GetValue<int?>("Revalidate:TimeoutSeconds") ?? 10;
    c.Timeout = TimeSpan.FromSeconds(seconds);
});

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var uploadsRoot = builder.Configuration["Uploads:RootPath"];

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();

if (!string.IsNullOrWhiteSpace(uploadsRoot) && Directory.Exists(uploadsRoot))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(Path.GetFullPath(uploadsRoot)),
        RequestPath = "/uploads",
        ServeUnknownFileTypes = false,
        OnPrepareResponse = ctx =>
        {
            ctx.Context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
        },
    });
}

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapAuthEndpoints();
app.MapTransportEndpoints();

app.Run();
