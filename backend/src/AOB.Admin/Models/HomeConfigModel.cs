using System.Text.Json;
using System.Text.Json.Serialization;

namespace AOB.Admin.Models;

/// <summary>
/// Modelo tipado para editar o campo jsonb <c>Site.HomeConfig</c> no backoffice.
/// Serializa para JSON compatível com o schema documentado no PLANO-RECONSTRUCAO §12.3.
/// </summary>
public class HomeConfigModel
{
    [JsonPropertyName("mission")]
    public string? Mission { get; set; }

    [JsonPropertyName("missionImageUrl")]
    public string? MissionImageUrl { get; set; }

    [JsonPropertyName("foundedYear")]
    public int? FoundedYear { get; set; }

    [JsonPropertyName("memberCount")]
    public int? MemberCount { get; set; }

    [JsonPropertyName("ringsPerYear")]
    public int? RingsPerYear { get; set; }

    [JsonPropertyName("speciesCount")]
    public int? SpeciesCount { get; set; }

    [JsonPropertyName("missionTitle")]
    public string? MissionTitle { get; set; }

    [JsonPropertyName("missionQuote")]
    public string? MissionQuote { get; set; }

    [JsonPropertyName("areas")]
    public List<AreaItem> Areas { get; set; } = new();

    [JsonPropertyName("benefits")]
    public List<BenefitItem> Benefits { get; set; } = new();

    [JsonPropertyName("ctaTitle")]
    public string? CtaTitle { get; set; }

    [JsonPropertyName("ctaSubtitle")]
    public string? CtaSubtitle { get; set; }

    [JsonPropertyName("ctaHref")]
    public string? CtaHref { get; set; }

    [JsonPropertyName("ctaLabel")]
    public string? CtaLabel { get; set; }

    public class AreaItem
    {
        [JsonPropertyName("icon")] public string Icon { get; set; } = "Award";
        [JsonPropertyName("title")] public string Title { get; set; } = "";
        [JsonPropertyName("description")] public string Description { get; set; } = "";
        [JsonPropertyName("href")] public string? Href { get; set; }
    }

    public class BenefitItem
    {
        [JsonPropertyName("icon")] public string Icon { get; set; } = "Award";
        [JsonPropertyName("label")] public string Label { get; set; } = "";
        [JsonPropertyName("description")] public string Description { get; set; } = "";
    }

    public static readonly string[] AllowedIcons =
    {
        "Award", "Calendar", "BookOpen", "Users", "Feather", "Trophy",
        "HeartHandshake", "Sparkles", "MapPin", "GraduationCap", "Landmark", "Newspaper",
        "ShieldCheck", "Dna", "Plane",
    };

    private static readonly JsonSerializerOptions Opts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    public static HomeConfigModel FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new HomeConfigModel();
        try
        {
            return JsonSerializer.Deserialize<HomeConfigModel>(json, Opts) ?? new HomeConfigModel();
        }
        catch
        {
            return new HomeConfigModel();
        }
    }

    public string ToJson() => JsonSerializer.Serialize(this, Opts);
}

/// <summary>
/// Modelo tipado para <c>Site.Announcement</c>.
/// </summary>
public class AnnouncementModel
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("href")]
    public string? Href { get; set; }

    [JsonPropertyName("tone")]
    public string Tone { get; set; } = "info";

    public static readonly string[] AllowedTones = { "info", "warning", "event" };

    private static readonly JsonSerializerOptions Opts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static AnnouncementModel FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new AnnouncementModel();
        try
        {
            return JsonSerializer.Deserialize<AnnouncementModel>(json, Opts) ?? new AnnouncementModel();
        }
        catch
        {
            return new AnnouncementModel();
        }
    }

    public string ToJson() => JsonSerializer.Serialize(this, Opts);
}
