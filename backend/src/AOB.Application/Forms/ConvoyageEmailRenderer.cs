using AOB.Application.Contracts;
using AOB.Core.Entities;

namespace AOB.Application.Forms;

// Renderers HTML dos emails da inscrição de convoyage. Usado tanto pelo
// endpoint público (primeira submissão) como pelo backoffice (reenvio após
// edição). Manter o corpo aqui evita divergência entre os dois caminhos.
public static class ConvoyageEmailRenderer
{
    public static string RenderAssociacao(
        Site site, InscricaoConvoyageRequest r, int submissionId, string localRecolha, int year,
        int numAvesVendaOferecidas = 0, int numAvesTransporteOferecidas = 0)
    {
        var header = $"""
        <h2 style="color:#1a4380;margin:0 0 8px">Nova inscrição de convoyage {year}</h2>
        <p style="color:#666;margin:0 0 16px">Submissão #{submissionId} · {H(site.Name)}</p>
        """;
        return header + RenderBody(r, localRecolha, forCriador: false,
            numAvesVendaOferecidas, numAvesTransporteOferecidas);
    }

    public static string RenderCriador(
        Site site, InscricaoConvoyageRequest r, int submissionId, string localRecolha, int year,
        int numAvesVendaOferecidas = 0, int numAvesTransporteOferecidas = 0)
    {
        var header = $"""
        <p>Olá {H(r.NomeCompleto)},</p>
        <p>Recebemos a tua inscrição na convoyage {year} da <b>{H(site.Name)}</b>. Segue em baixo o resumo dos dados que submeteste; o detalhe completo das aves e dos custos vai em anexo (PDF).</p>
        <p style="color:#666;margin:0 0 16px;font-size:12px">Referência: submissão #{submissionId}</p>
        """;
        var footer = $"""
        <p style="margin-top:20px">Cumprimentos,<br/>{H(site.Name)}</p>
        <hr style="border:none;border-top:1px solid #eee;margin:24px 0"/>
        <p style="color:#888;font-size:12px">Este é um email automático. Se não foste tu a submeter esta inscrição, por favor contacta-nos.</p>
        """;
        return header + RenderBody(r, localRecolha, forCriador: true,
            numAvesVendaOferecidas, numAvesTransporteOferecidas) + footer;
    }

    private static string RenderBody(InscricaoConvoyageRequest r, string localRecolha, bool forCriador,
        int numAvesVendaOferecidas = 0, int numAvesTransporteOferecidas = 0)
    {
        var totalAves = r.Aves?.Count ?? 0;
        var totalVenda = r.AvesVenda?.Count ?? 0;
        var totalTransporte = r.AvesTransporte?.Count ?? 0;
        var numTranspCompra = r.AvesTransporte?.Count(a => a.Origem == OrigemAveTransporte.Compra) ?? 0;
        var numTranspVende = r.AvesTransporte?.Count(a => a.Origem == OrigemAveTransporte.Vende) ?? 0;
        var espacosTransp = ConvoyagePricing.EspacosTransporteAdquirido(numTranspCompra, numTranspVende);
        var vendaOferecidas = Math.Clamp(numAvesVendaOferecidas, 0, totalVenda);
        var espacosOferecidos = Math.Clamp(numAvesTransporteOferecidas, 0, espacosTransp);

        var dadosHeader = forCriador ? "Dados submetidos" : "Dados do criador";

        var pagamento = forCriador
            ? "<div style=\"margin-top:12px;padding:10px 12px;background:#fff8e1;border-left:4px solid #f5a623;font-size:13px\"><b>Pagamento:</b> deve ser feito no valor certo, em dinheiro, num envelope fechado, e entregue juntamente com as aves.</div>"
            : "";

        var notasTransporte = totalTransporte > 0
            ? "<div style=\"margin-top:12px;padding:8px 12px;background:#fff8e1;border-left:4px solid #f5a623;font-size:12px\"><b>Notas sobre as aves de compra/venda:</b><ul style=\"margin:6px 0 0 18px;padding:0\"><li><b>Chegada prevista: 12h (hora belga).</b> O destinatário tem obrigatoriamente de estar presente a essa hora para receber as aves — <b>não há condições para lhes dar água e alimentação</b> e não nos responsabilizamos por eventuais mortes.</li><li><b>Se comprou aves na Bélgica:</b> o remetente indicado tem de entregar as aves à convoyage portuguesa <b>no domingo de manhã</b>, e não antes — <b>não há condições para dar água e alimentação às aves</b> e não nos responsabilizamos por eventuais mortes.</li><li>Sujeito a validação — limite total de <b>400 aves para transporte</b>, prioridade para aves de exposição. Confirmação por email <b>após o fecho das inscrições</b>.</li></ul></div>"
            : "";

        var anexoNota = forCriador
            ? "<p style=\"margin-top:16px;color:#666;font-size:12px\">Vai anexo o PDF com o detalhe completo das aves e o resumo de custos. Guarda-o para tua referência.</p>"
            : "<p style=\"margin-top:16px;color:#666;font-size:12px\">O detalhe das aves e o resumo de custos estão no PDF em anexo.</p>";

        return $"""
        <h3>{dadosHeader}</h3>
        <table cellpadding="4" style="border-collapse:collapse;font-size:13px">
          <tr><td><b>Nome:</b></td><td>{H(r.NomeCompleto)}</td></tr>
          <tr><td><b>País:</b></td><td>{H(r.Pais)}</td></tr>
          <tr><td><b>Email:</b></td><td>{H(r.Email)}</td></tr>
          <tr><td><b>Telefone:</b></td><td>{H(r.Telefone)}</td></tr>
          <tr><td><b>Nº STAM:</b></td><td>{H(r.NumeroStam)}</td></tr>
          <tr><td valign="top"><b>Local de recolha:</b></td><td>{H(localRecolha)}<br/><span style="color:#b45309;font-size:12px">Contacta o responsável do ponto de recolha para combinar a hora de entrega das aves.</span></td></tr>
        </table>

        <h3>Resumo</h3>
        <table cellpadding="4" style="border-collapse:collapse;font-size:13px">
          <tr><td><b>Aves para concurso:</b></td><td>{totalAves}</td></tr>
          {(totalVenda > 0 ? $"<tr><td><b>Aves para venda:</b></td><td>{totalVenda}{(vendaOferecidas > 0 ? $" <span style=\"color:#166534\">(dos quais {vendaOferecidas} oferecid{(vendaOferecidas == 1 ? "a" : "as")}, isent{(vendaOferecidas == 1 ? "a" : "as")} de pagamento)</span>" : "")}</td></tr>" : "")}
          {(totalTransporte > 0 ? $"<tr><td valign=\"top\"><b>Aves para transporte (compra/venda):</b></td><td>{totalTransporte} <span style=\"color:#666\">({numTranspCompra} compra + {numTranspVende} venda · <b>{espacosTransp} espaço{(espacosTransp == 1 ? "" : "s")}</b> a pagar)</span>{(espacosOferecidos > 0 ? $"<br/><span style=\"color:#166534\">Dos quais {espacosOferecidos} espaço{(espacosOferecidos == 1 ? "" : "s")} oferecid{(espacosOferecidos == 1 ? "o" : "os")}, isent{(espacosOferecidos == 1 ? "o" : "os")} de pagamento</span>" : "")} <span style=\"color:#b45309\">(sujeitas a validação de espaço)</span></td></tr>" : "")}
        </table>

        {notasTransporte}
        {pagamento}
        {anexoNota}
        """;
    }

    private static string H(string? s) => System.Net.WebUtility.HtmlEncode(s ?? "");
}
