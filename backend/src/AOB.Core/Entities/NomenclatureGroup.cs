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
    Roseicollis = 0,
    Personatus  = 1,
    Fischeri    = 2,
    Nigrigenis  = 3,
    Lilianae    = 4,
    Canus       = 5,
    Taranta     = 6,
    Pullarius   = 7,
}

[JsonConverter(typeof(JsonStringEnumConverter<EntryTypeCode>))]
public enum EntryTypeCode
{
    Individual = 0,
    Team       = 1,
    Study      = 2,
}
