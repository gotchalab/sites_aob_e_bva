namespace AOB.Api.Auth;

public class JwtOptions
{
    public string Issuer { get; set; } = "aob";
    public string Audience { get; set; } = "aob-clients";
    public string SigningKey { get; set; } = "";
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 30;
}
