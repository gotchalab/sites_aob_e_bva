using AOB.Core.Entities;

namespace AOB.Application.Contracts;

// ── PUBLIC ──────────────────────────────────────────────────────────────────

public record NomenclatureClassDto(
    int Id,
    string Code,
    string Mutation,
    string? Notes);

public record NomenclatureGroupDto(
    int Id,
    string CodePrefix,
    string DisplayName,
    SpeciesCode Species,
    EntryTypeCode EntryType,
    List<NomenclatureClassDto> Classes);

public record NomenclatureVersionDto(
    int ConvoyageYearId,
    int Year,
    List<NomenclatureGroupDto> Groups);

// ── ADMIN ───────────────────────────────────────────────────────────────────

public record NomenclatureGroupAdminDto(
    int Id,
    string CodePrefix,
    string DisplayName,
    SpeciesCode Species,
    EntryTypeCode EntryType,
    int SortOrder,
    int ClassCount);

public record CreateGroupRequest(
    string CodePrefix,
    string DisplayName,
    SpeciesCode Species,
    EntryTypeCode EntryType,
    int SortOrder);

public record UpdateGroupRequest(
    string DisplayName,
    int SortOrder);

public record ClassAdminDto(
    int Id,
    string Code,
    string Mutation,
    int SortOrder,
    bool IsActive,
    string? Notes);

public record CreateClassRequest(
    string Code,
    string Mutation,
    int SortOrder,
    string? Notes);

public record UpdateClassRequest(
    string Code,
    string Mutation,
    int SortOrder,
    bool IsActive,
    string? Notes);

public record CloneNomenclatureRequest(int SourceConvoyageYearId);
