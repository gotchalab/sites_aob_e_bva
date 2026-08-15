using AOB.Admin.Components;
using AOB.Admin.Services;
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
builder.Services.AddHttpClient<RevalidateNotifier>();

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
