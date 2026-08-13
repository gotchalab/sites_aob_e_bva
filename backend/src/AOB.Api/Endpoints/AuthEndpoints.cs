using System.Security.Claims;
using AOB.Api.Auth;
using AOB.Core.Entities;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AOB.Api.Endpoints;

public record LoginRequest(string Email, string Password);
public record RefreshRequest(string UserId, string RefreshToken);
public record TokenResponse(string AccessToken, string RefreshToken, DateTime ExpiresAt,
    string UserId, string Email, string FullName, string[] Roles, int? SocioId);
public record MeResponse(string UserId, string Email, string FullName, string[] Roles, int? SocioId);

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuth(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/auth").WithTags("Auth");

        g.MapPost("/login", async (
            [FromBody] LoginRequest req,
            SignInManager<ApplicationUser> signIn,
            UserManager<ApplicationUser> users,
            JwtService jwt) =>
        {
            var user = await users.FindByEmailAsync(req.Email);
            if (user is null)
                return Results.Unauthorized();
            var check = await signIn.CheckPasswordSignInAsync(user, req.Password, lockoutOnFailure: true);
            if (!check.Succeeded)
                return Results.Unauthorized();

            user.LastLoginAt = DateTime.UtcNow;
            await users.UpdateAsync(user);

            var tokens = await jwt.IssueAsync(user);
            var roles = await users.GetRolesAsync(user);
            return Results.Ok(new TokenResponse(
                tokens.AccessToken, tokens.RefreshToken, tokens.AccessExpires,
                user.Id, user.Email ?? "", user.FullName, roles.ToArray(), user.SocioId));
        });

        g.MapPost("/refresh", async (
            [FromBody] RefreshRequest req,
            UserManager<ApplicationUser> users,
            JwtService jwt) =>
        {
            var tokens = await jwt.RefreshAsync(req.UserId, req.RefreshToken);
            if (tokens is null) return Results.Unauthorized();
            var user = await users.FindByIdAsync(req.UserId);
            var roles = user is null ? Array.Empty<string>() : (await users.GetRolesAsync(user)).ToArray();
            return Results.Ok(new TokenResponse(
                tokens.AccessToken, tokens.RefreshToken, tokens.AccessExpires,
                req.UserId, user?.Email ?? "", user?.FullName ?? "", roles, user?.SocioId));
        });

        g.MapPost("/logout", async (
            ClaimsPrincipal principal,
            UserManager<ApplicationUser> users,
            JwtService jwt) =>
        {
            var userId = principal.FindFirstValue("sub") ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null) return Results.Unauthorized();
            var user = await users.FindByIdAsync(userId);
            if (user is not null) await jwt.RevokeAsync(user);
            return Results.NoContent();
        }).RequireAuthorization();

        g.MapGet("/me", (ClaimsPrincipal p) =>
        {
            var userId = p.FindFirstValue("sub") ?? p.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            var roles = p.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray();
            var socioId = int.TryParse(p.FindFirstValue("socio_id"), out var s) ? s : (int?)null;
            return TypedResults.Ok(new MeResponse(
                userId,
                p.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email) ?? "",
                p.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Name) ?? "",
                roles, socioId));
        }).RequireAuthorization();

        return g;
    }
}
