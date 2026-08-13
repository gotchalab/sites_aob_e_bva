using System.Security.Claims;
using AOB.Application.Contracts;
using AOB.Core.Entities;
using AOB.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AOB.Api.Endpoints;

public static class MeEndpoints
{
    public static RouteGroupBuilder MapMe(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/me")
            .WithTags("Socio")
            .RequireAuthorization("Socio");

        g.MapGet("", GetProfile);
        g.MapPut("", UpdateProfile);
        g.MapGet("/quotas", GetQuotas);
        g.MapGet("/anilhas", GetAnilhas);
        g.MapPost("/pedidos-anilhas", CreatePedidoAnilha);

        return g;
    }

    private static int? SocioIdOf(ClaimsPrincipal p) =>
        int.TryParse(p.FindFirstValue("socio_id"), out var s) ? s : null;

    private static async Task<Results<Ok<SocioProfileDto>, ForbidHttpResult, NotFound>> GetProfile(
        ClaimsPrincipal principal, AppDbContext db)
    {
        var id = SocioIdOf(principal);
        if (id is null) return TypedResults.Forbid();

        var s = await db.Socios.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id.Value);
        if (s is null) return TypedResults.NotFound();
        return TypedResults.Ok(ToDto(s));
    }

    private static async Task<Results<Ok<SocioProfileDto>, ForbidHttpResult, NotFound>> UpdateProfile(
        [FromBody] UpdateSocioProfileRequest req,
        ClaimsPrincipal principal, AppDbContext db)
    {
        var id = SocioIdOf(principal);
        if (id is null) return TypedResults.Forbid();

        var s = await db.Socios.FirstOrDefaultAsync(x => x.Id == id.Value);
        if (s is null) return TypedResults.NotFound();

        if (!string.IsNullOrWhiteSpace(req.NomeCompleto)) s.NomeCompleto = req.NomeCompleto.Trim();
        s.Telefone = req.Telefone;
        s.NIF = req.NIF;
        s.DataNascimento = req.DataNascimento;
        s.Morada = req.Morada;
        s.CodigoPostal = req.CodigoPostal;
        s.Localidade = req.Localidade;
        if (req.EspeciesInteresse is not null) s.EspeciesInteresse = req.EspeciesInteresse;
        s.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return TypedResults.Ok(ToDto(s));
    }

    private static async Task<Results<Ok<List<QuotaDto>>, ForbidHttpResult>> GetQuotas(
        ClaimsPrincipal principal, AppDbContext db)
    {
        var id = SocioIdOf(principal);
        if (id is null) return TypedResults.Forbid();
        var list = await db.Quotas.AsNoTracking()
            .Where(q => q.SocioId == id.Value)
            .OrderByDescending(q => q.Ano)
            .Select(q => new QuotaDto(q.Id, q.Ano, q.Valor, q.DataPagamento, q.Metodo, q.Recibo))
            .ToListAsync();
        return TypedResults.Ok(list);
    }

    private static async Task<Results<Ok<List<PedidoAnilhaDto>>, ForbidHttpResult>> GetAnilhas(
        ClaimsPrincipal principal, AppDbContext db)
    {
        var id = SocioIdOf(principal);
        if (id is null) return TypedResults.Forbid();
        var list = await db.PedidosAnilha.AsNoTracking()
            .Where(p => p.SocioId == id.Value)
            .OrderByDescending(p => p.DataPedido)
            .Select(p => new PedidoAnilhaDto(
                p.Id, p.EspecieCientifica, p.EspecieNomeComum,
                p.Ano, p.Diametro, p.Quantidade,
                p.Estado.ToString(), p.DataPedido, p.DataEntrega, p.Observacoes))
            .ToListAsync();
        return TypedResults.Ok(list);
    }

    private static async Task<Results<Ok<PedidoAnilhaDto>, ForbidHttpResult, BadRequest<string>>> CreatePedidoAnilha(
        [FromBody] CreatePedidoAnilhaRequest req,
        ClaimsPrincipal principal, AppDbContext db)
    {
        var id = SocioIdOf(principal);
        if (id is null) return TypedResults.Forbid();

        if (string.IsNullOrWhiteSpace(req.EspecieCientifica) || req.Ano < 2020 || req.Quantidade < 1)
            return TypedResults.BadRequest("Dados invalidos");

        var pedido = new PedidoAnilha
        {
            SocioId = id.Value,
            EspecieCientifica = req.EspecieCientifica.Trim(),
            EspecieNomeComum = req.EspecieNomeComum,
            Ano = req.Ano,
            Diametro = req.Diametro,
            Quantidade = req.Quantidade,
            Observacoes = req.Observacoes,
            Estado = PedidoAnilhaEstado.Pendente,
            DataPedido = DateTime.UtcNow,
        };
        db.PedidosAnilha.Add(pedido);
        await db.SaveChangesAsync();

        return TypedResults.Ok(new PedidoAnilhaDto(
            pedido.Id, pedido.EspecieCientifica, pedido.EspecieNomeComum,
            pedido.Ano, pedido.Diametro, pedido.Quantidade,
            pedido.Estado.ToString(), pedido.DataPedido, pedido.DataEntrega, pedido.Observacoes));
    }

    private static SocioProfileDto ToDto(Socio s) => new(
        s.Id, s.NumeroSocio, s.NomeCompleto, s.Email, s.Telefone, s.NIF,
        s.DataNascimento, s.Morada, s.CodigoPostal, s.Localidade,
        s.FotoPath, s.EspeciesInteresse, s.DataInscricao, s.Estado.ToString());
}
