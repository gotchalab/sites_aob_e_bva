using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AOB.Admin.Services;

/// Traduz excepcoes tecnicas (EF Core / PostgreSQL) em mensagens compreensiveis
/// para o utilizador do backoffice. Usar em catch blocks das paginas Razor.
public static class FriendlyErrors
{
    public static string Describe(Exception ex)
    {
        // EF Core envolve a Npgsql exception em DbUpdateException.
        var pg = FindPostgresException(ex);
        if (pg is not null)
        {
            return pg.SqlState switch
            {
                // 23505 = unique_violation. O ConstraintName vem tipo
                // "IX_categories_SiteId_Slug" — extrair coluna(s) para msg amigavel.
                "23505" => DescribeUniqueViolation(pg),
                // 23503 = foreign_key_violation
                "23503" => "Nao e possivel guardar/eliminar: existe outra entidade que depende desta.",
                // 23502 = not_null_violation
                "23502" => $"Campo obrigatorio em falta ({pg.ColumnName ?? "nao especificado"}).",
                _ => pg.MessageText ?? ex.Message,
            };
        }
        return ex.Message;
    }

    private static string DescribeUniqueViolation(PostgresException pg)
    {
        var name = pg.ConstraintName ?? "";
        // "IX_categories_SiteId_Slug" → menciona "Slug" (o ultimo componente).
        if (name.Contains("Slug", StringComparison.OrdinalIgnoreCase))
            return "Ja existe um registo com o mesmo slug neste site. Altere o slug e tente novamente.";
        if (name.Contains("Email", StringComparison.OrdinalIgnoreCase))
            return "Ja existe um registo com este email.";
        return "Ja existe um registo com estes dados (violacao de chave unica).";
    }

    private static PostgresException? FindPostgresException(Exception? ex)
    {
        while (ex is not null)
        {
            if (ex is PostgresException pg) return pg;
            ex = ex.InnerException;
        }
        return null;
    }
}
