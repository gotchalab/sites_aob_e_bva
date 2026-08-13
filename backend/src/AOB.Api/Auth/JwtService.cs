using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AOB.Core.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AOB.Api.Auth;

public record TokenPair(string AccessToken, string RefreshToken, DateTime AccessExpires);

public class JwtService(
    IOptions<JwtOptions> options,
    UserManager<ApplicationUser> users)
{
    private readonly JwtOptions _opts = options.Value;

    public async Task<TokenPair> IssueAsync(ApplicationUser user)
    {
        var handler = new JwtSecurityTokenHandler();
        var creds = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opts.SigningKey)),
            SecurityAlgorithms.HmacSha256);

        var roles = await users.GetRolesAsync(user);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email ?? ""),
            new(JwtRegisteredClaimNames.Name, user.FullName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        if (user.SocioId is int sid) claims.Add(new Claim("socio_id", sid.ToString()));

        var expires = DateTime.UtcNow.AddMinutes(_opts.AccessTokenMinutes);
        var jwt = new JwtSecurityToken(
            issuer: _opts.Issuer,
            audience: _opts.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expires,
            signingCredentials: creds);

        var access = handler.WriteToken(jwt);
        var refresh = GenerateRefreshToken();

        // Guardamos o refresh token como AuthenticationToken do Identity
        await users.RemoveAuthenticationTokenAsync(user, "aob", "refresh");
        await users.SetAuthenticationTokenAsync(user, "aob", "refresh", refresh);

        return new TokenPair(access, refresh, expires);
    }

    public async Task<TokenPair?> RefreshAsync(string userId, string refreshToken)
    {
        var user = await users.FindByIdAsync(userId);
        if (user is null) return null;
        var stored = await users.GetAuthenticationTokenAsync(user, "aob", "refresh");
        if (stored is null || !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(stored),
                Encoding.UTF8.GetBytes(refreshToken)))
            return null;
        return await IssueAsync(user);
    }

    public async Task RevokeAsync(ApplicationUser user)
    {
        await users.RemoveAuthenticationTokenAsync(user, "aob", "refresh");
    }

    private static string GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(48);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }
}
