using System.Text.Json.Serialization;

namespace AOB.Core.Entities;

public class NomenclatureGroup
{
    public int Id { get; set; }

    public int ConvoyageYearId { get; set; }
    public ConvoyageYear ConvoyageYear { get; set; } = null!;

    public SpeciesCode Species { get; set; }
    public EntryTypeCode EntryType { get; set; }

    // "012", "055", "451" — 3-digit prefix shared by all classes in this group.
    public string CodePrefix { get; set; } = "";

    // Display label for the group ("Pallid", "Dominant Edged", "Mutations").
    public string DisplayName { get; set; } = "";

    public int SortOrder { get; set; }

    public ICollection<NomenclatureClass> Classes { get; set; } = [];
}

[JsonConverter(typeof(JsonStringEnumConverter<SpeciesCode>))]
public enum SpeciesCode
{
    // Agapornis (géneros 0-7)
    Roseicollis = 0,
    Personatus  = 1,
    Fischeri    = 2,
    Nigrigenis  = 3,
    Lilianae    = 4,
    Canus       = 5,
    Taranta     = 6,
    Pullarius   = 7,
    // Forpus (8+)
    Coelestis   = 8,
}

// Mapa espécie → nome do género. Usado para renderizar nomes binomiais
// completos ("Agapornis roseicollis", "Forpus coelestis") em PDFs, emails e
// labels. Se acrescentares uma nova espécie no enum, acrescenta aqui também.
public static class SpeciesGenus
{
    private static readonly Dictionary<SpeciesCode, string> Map = new()
    {
        [SpeciesCode.Roseicollis] = "Agapornis",
        [SpeciesCode.Personatus]  = "Agapornis",
        [SpeciesCode.Fischeri]    = "Agapornis",
        [SpeciesCode.Nigrigenis]  = "Agapornis",
        [SpeciesCode.Lilianae]    = "Agapornis",
        [SpeciesCode.Canus]       = "Agapornis",
        [SpeciesCode.Taranta]     = "Agapornis",
        [SpeciesCode.Pullarius]   = "Agapornis",
        [SpeciesCode.Coelestis]   = "Forpus",
    };

    public static string Of(SpeciesCode s) => Map.TryGetValue(s, out var g) ? g : "";

    // "Agapornis Roseicollis" — nome do género + espécie com capital, para
    // documentos legais (declaração TRACES).
    public static string Full(SpeciesCode s) => $"{Of(s)} {s}".Trim();

    // "A. roseicollis" — inicial do género + espécie em minúsculas, para
    // labels de UI/PDF onde o espaço é curto.
    public static string Short(SpeciesCode s)
    {
        var g = Of(s);
        var initial = string.IsNullOrEmpty(g) ? "" : g[0] + ".";
        return $"{initial} {s.ToString().ToLowerInvariant()}".Trim();
    }
}

[JsonConverter(typeof(JsonStringEnumConverter<EntryTypeCode>))]
public enum EntryTypeCode
{
    Individual = 0,
    Team       = 1,
    Study      = 2,
}
