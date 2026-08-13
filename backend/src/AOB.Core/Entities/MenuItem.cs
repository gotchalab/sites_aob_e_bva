namespace AOB.Core.Entities;

public class MenuItem
{
    public int Id { get; set; }
    public int SiteId { get; set; }
    public Site Site { get; set; } = null!;

    public int? ParentId { get; set; }
    public MenuItem? Parent { get; set; }
    public ICollection<MenuItem> Children { get; set; } = new List<MenuItem>();

    public string MenuType { get; set; } = "mainmenu";
    public string Title { get; set; } = "";
    public string? Url { get; set; }

    public MenuTargetType TargetType { get; set; } = MenuTargetType.Internal;
    public int? TargetId { get; set; }

    public int SortOrder { get; set; }
    public bool IsPublished { get; set; } = true;
    public bool OpenInNewTab { get; set; }

    public int? LegacyId { get; set; }
}

public enum MenuTargetType
{
    Internal = 0,
    External = 1,
    Article = 2,
    Category = 3,
    Download = 4,
}
