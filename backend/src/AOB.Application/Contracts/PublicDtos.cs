namespace AOB.Application.Contracts;

public record SiteDto(int Id, string Slug, string Name, string Domain, string? Description,
    string? LogoUrl, string? PrimaryColor, string? ContactEmail,
    string? Tagline, string? HomeConfig, string? Announcement);

public record SponsorDto(int Id, string Name, string Slug, string LogoPath, string? ClickUrl,
    string Tier, int SortOrder);

public record CategoryDto(int Id, string Slug, string Name, string? Description, int? ParentId, int SortOrder);

public record CategoryTreeDto(int Id, string Slug, string Name, string? Description,
    int SortOrder, List<CategoryTreeDto> Children);

public record ArticleSummaryDto(int Id, string Slug, string Title, string? Excerpt,
    string? CoverImagePath, DateTime? PublishedAt, string CategorySlug, string CategoryName,
    bool IsFeatured);

public record ArticleDetailDto(int Id, string Slug, string Title, string? Excerpt, string Content,
    string? CoverImagePath, DateTime? PublishedAt, string CategorySlug, string CategoryName,
    string? MetaTitle, string? MetaDescription, int ViewCount, List<string> Tags);

public record DownloadSummaryDto(int Id, string Slug, string Title, string? Description,
    string FileName, long FileSize, string MimeType, DateTime? PublishedAt,
    int DownloadCount, string? CategorySlug, string? CategoryName);

public record DownloadDetailDto(int Id, string Slug, string Title, string? Description,
    string FileName, string StoragePath, long FileSize, string MimeType,
    DateTime? PublishedAt, int DownloadCount, string? CategorySlug, string? CategoryName);

public record MenuItemDto(int Id, string Title, string? Url, string TargetType, int? TargetId,
    int SortOrder, bool OpenInNewTab, List<MenuItemDto> Children);

public record PagedResult<T>(int Total, int Page, int PageSize, IReadOnlyList<T> Items);
