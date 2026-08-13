using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AOB.Application.Forms;

/// <summary>
/// Verifica um token Cloudflare Turnstile no server.
/// Se secret nao estiver configurado, faz bypass em dev (avisa nos logs).
/// </summary>
public class TurnstileVerifier(
    IHttpClientFactory httpFactory,
    IConfiguration config,
    ILogger<TurnstileVerifier> log)
{
    private const string VerifyUrl = "https://challenges.cloudflare.com/turnstile/v0/siteverify";

    public async Task<bool> VerifyAsync(string? token, string? remoteIp, CancellationToken ct = default)
    {
        var secret = config["Turnstile:SecretKey"];
        if (string.IsNullOrWhiteSpace(secret))
        {
            log.LogWarning("Turnstile:SecretKey nao configurado — a fazer bypass (APENAS DEV)");
            return true;
        }
        if (string.IsNullOrWhiteSpace(token)) return false;

        var http = httpFactory.CreateClient();
        var payload = new Dictionary<string, string>
        {
            ["secret"] = secret,
            ["response"] = token,
        };
        if (!string.IsNullOrWhiteSpace(remoteIp)) payload["remoteip"] = remoteIp;

        try
        {
            var res = await http.PostAsync(VerifyUrl, new FormUrlEncodedContent(payload), ct);
            var body = await res.Content.ReadFromJsonAsync<TurnstileResponse>(cancellationToken: ct);
            if (body?.Success == true) return true;
            log.LogWarning("Turnstile falhou: {Errors}", string.Join(",", body?.ErrorCodes ?? Array.Empty<string>()));
            return false;
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Erro ao verificar Turnstile");
            return false;
        }
    }

    private class TurnstileResponse
    {
        public bool Success { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("error-codes")]
        public string[]? ErrorCodes { get; set; }
    }
}
