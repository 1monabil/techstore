namespace TechStore.Infrastructure.Seed;

/// <summary>Fixed reference data: category tree and brands. Ids are stable so seeds are idempotent.</summary>
public static class CatalogReference
{
    public sealed record CategoryDef(int Id, string Name, string Slug, int? ParentId, int SortOrder);
    public sealed record BrandDef(int Id, string Name, string Slug);

    public static readonly CategoryDef[] Categories =
    [
        new(1, "Computers", "computers", null, 1),
        new(10, "Laptops", "laptops", 1, 1),
        new(11, "Tablets", "tablets", 1, 2),
        new(2, "Phones", "phones", null, 2),
        new(20, "Smartphones", "smartphones", 2, 1),
        new(3, "Components", "components", null, 3),
        new(30, "Graphics Cards", "graphics-cards", 3, 1),
        new(31, "Processors", "processors", 3, 2),
        new(32, "Motherboards", "motherboards", 3, 3),
        new(33, "RAM", "ram", 3, 4),
        new(34, "SSDs", "ssds", 3, 5),
        new(35, "HDDs", "hdds", 3, 6),
        new(36, "Power Supplies", "power-supplies", 3, 7),
        new(37, "Cases", "cases", 3, 8),
        new(4, "Peripherals", "peripherals", null, 4),
        new(40, "Monitors", "monitors", 4, 1),
        new(41, "Keyboards", "keyboards", 4, 2),
        new(42, "Mice", "mice", 4, 3),
        new(43, "Headsets", "headsets", 4, 4),
        new(5, "Networking", "networking", null, 5),
        new(50, "Routers", "routers", 5, 1),
        new(51, "Switches", "switches", 5, 2),
    ];

    public static readonly BrandDef[] Brands =
    [
        new(1, "ASUS", "asus"), new(2, "MSI", "msi"), new(3, "Gigabyte", "gigabyte"), new(4, "Dell", "dell"),
        new(5, "HP", "hp"), new(6, "Lenovo", "lenovo"), new(7, "Apple", "apple"), new(8, "Samsung", "samsung"),
        new(9, "Logitech", "logitech"), new(10, "Corsair", "corsair"), new(11, "Razer", "razer"),
        new(12, "Kingston", "kingston"), new(13, "Western Digital", "western-digital"), new(14, "Seagate", "seagate"),
        new(15, "AMD", "amd"), new(16, "Intel", "intel"), new(17, "NVIDIA", "nvidia"), new(18, "TP-Link", "tp-link"),
        new(19, "Crucial", "crucial"), new(20, "LG", "lg"), new(21, "SteelSeries", "steelseries"),
        new(22, "HyperX", "hyperx"), new(23, "Lian Li", "lian-li"), new(24, "NZXT", "nzxt"),
        new(25, "Fractal Design", "fractal-design"), new(26, "Seasonic", "seasonic"), new(27, "be quiet!", "be-quiet"),
        new(28, "Netgear", "netgear"), new(29, "Google", "google"), new(30, "Xiaomi", "xiaomi"),
        new(31, "G.Skill", "g-skill"), new(32, "Alienware", "alienware"),
    ];

    public static string PathOf(CategoryDef c)
    {
        var parts = new List<string> { c.Slug };
        var cur = c;
        while (cur.ParentId is { } pid)
        {
            cur = Categories.First(x => x.Id == pid);
            parts.Insert(0, cur.Slug);
        }
        return string.Join('/', parts);
    }

    public static CategoryDef Category(string slug) => Categories.First(c => c.Slug == slug);
    public static BrandDef Brand(string name) => Brands.First(b => b.Name == name);
}
