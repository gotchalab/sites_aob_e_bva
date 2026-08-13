using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AOB.Api.Endpoints;

/// <summary>
/// Reencaminha um pedido de revalidação para o frontend Next.js correspondente.
/// Chamado pelo backoffice quando o admin publica/edita conteúdo — invalida o cache ISR
/// imediatamente em vez de esperar pelos 60/300s.
/// </summary>
public static class RevalidateEndpoints
{
    public static void MapRevalidate(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/revalidate", async (
            string siteSlug,
            string? path,
            string? secret,
            IHttpClientFactory httpFactory,
            IConfiguration cfg,
            ILogger<RevalidateMarker> log) =>
        {
            var expected = cfg["Revalidate:Secret"];
            if (!string.IsNullOrEmpty(expected) && secret != expected)
                return Results.Unauthorized();

            var target = siteSlug switch
            {
                "aob" => cfg["Revalidate:AobUrl"],
                "bva" => cfg["Revalidate:BvaUrl"],
                _ => null,
            };
            if (string.IsNullOrWhiteSpace(target))
                return Results.BadRequest(new { error = "Site desconhecido ou sem URL de revalidate configurada." });

            var normalized = string.IsNullOrWhiteSpace(path) ? "/" : path;
            var url = $"{target.TrimEnd('/')}/api/revalidate?path={Uri.EscapeDataString(normalized)}";
            var frontendSecret = siteSlug == "aob" ? cfg["Revalidate:AobSecret"] : cfg["Revalidate:BvaSecret"];
            if (!string.IsNullOrEmpty(frontendSecret))
                url += $"&secret={Uri.EscapeDataString(frontendSecret)}";

            try
            {
                using var http = httpFactory.CreateClient();
                http.Timeout = TimeSpan.FromSeconds(5);
                var res = await http.PostAsync(url, content: null);
                if (!res.IsSuccessStatusCode)
                {
                    log.LogWarning("Revalidate {Site} {Path} devolveu {Status}", siteSlug, normalized, res.StatusCode);
                    return Results.Problem($"Frontend devolveu {(int)res.StatusCode}", statusCode: 502);
                }
                return Results.Ok(new { revalidated = true, path = normalized });
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Falha ao contactar frontend {Site} para revalidate", siteSlug);
                return Results.Problem("Falha ao contactar frontend", statusCode: 502);
            }
        }).WithTags("Revalidate");
    }

    private sealed class RevalidateMarker { }
}
