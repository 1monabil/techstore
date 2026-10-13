namespace TechStore.Infrastructure.Seed;

/// <summary>Real product lines per category, with plausible prices and specs (2024–2025 market).</summary>
/// <remarks>
/// Documented exception to the "≤ 4 parameters" rule: the per-category local functions (Laptop, Tab,
/// Card…) take many positional arguments on purpose. Each call is one row of catalogue data, and a
/// row-per-line table reads and diffs far better than ~120 object initialisers would.
/// </remarks>
public static class CatalogTemplates
{
    private static readonly VariantOption[] None = [new("", 0, _ => { })];

    // ---------- option helpers ----------
    private static VariantOption Ram(int gb, decimal delta, string? suffix = null) =>
        new($"{gb}GB{suffix}", delta, d => d.RamGb = gb);

    private static VariantOption Storage(int gb, decimal delta, string type = "NVMe") =>
        new(Units.StorageLabel(gb), delta, d => { d.StorageGb = gb; d.StorageType = type; });


    private static VariantOption Cpu(string brand, string model, string label, decimal delta) =>
        new(label, delta, d => { d.CpuBrand = brand; d.CpuModel = model; });

    private static VariantOption Gpu(string brand, string model, string label, decimal delta, int? vramGb = null) =>
        new(label, delta, d =>
        {
            d.GpuBrand = brand;
            d.GpuModel = model;
            if (vramGb is { } v) d.Attributes["VRAM"] = $"{v} GB";
        });

    private static VariantOption Color(string color, decimal delta = 0, bool inName = true) =>
        new(inName ? color : "", delta, d => d.Color = color);

    private static VariantOption Conn(string label, string connectivity, decimal delta) =>
        new(label, delta, d => d.Connectivity = connectivity);

    // ---------- lines ----------
    public static readonly IReadOnlyList<ProductLine> Lines = BuildLines();

    public static IEnumerable<ProductLine> ForCategory(string slug) => Lines.Where(l => l.Category == slug);

    private static List<ProductLine> BuildLines()
    {
        var l = new List<ProductLine>();
        Laptops(l); Tablets(l); Smartphones(l); GraphicsCards(l); Processors(l); Motherboards(l);
        Memory(l); Ssds(l); Hdds(l); PowerSupplies(l); Cases(l); Monitors(l); Keyboards(l);
        Mice(l); Headsets(l); Routers(l); Switches(l);
        return l;
    }

    private static void Laptops(List<ProductLine> l)
    {
        ProductLine Laptop(string brand, string model, decimal screen, int hz, decimal price, string panel,
            VariantOption[] cpus, VariantOption[] gpus, VariantOption[] rams, VariantOption[] storage, int curated = 3) =>
            new("laptops", brand, model, price, d =>
            {
                d.ScreenSizeInches = screen; d.RefreshRateHz = hz; d.Connectivity = "Wi-Fi 6E";
                d.Attributes["Panel"] = panel; d.Attributes["Operating system"] = brand == "Apple" ? "macOS" : "Windows 11 Home";
                d.Highlights.Add($"{screen}\" {panel} display at {hz}Hz");
            }, [cpus, gpus, rams, storage], curated);

        var i7hx = Cpu("Intel", "Intel Core i7-14650HX", "i7-14650HX", 0);
        var i9hx = Cpu("Intel", "Intel Core i9-14900HX", "i9-14900HX", 250);
        var ultra7 = Cpu("Intel", "Intel Core Ultra 7 155H", "Core Ultra 7", 0);
        var ultra9 = Cpu("Intel", "Intel Core Ultra 9 185H", "Core Ultra 9", 200);
        var r7 = Cpu("AMD", "AMD Ryzen 7 8845HS", "Ryzen 7 8845HS", 0);
        var r9 = Cpu("AMD", "AMD Ryzen 9 8945HS", "Ryzen 9 8945HS", 180);
        var arc = Gpu("Intel", "Intel Arc Graphics", "", 0);
        var radeon = Gpu("AMD", "AMD Radeon 780M", "", 0);
        VariantOption Rtx(string m, decimal delta, int vram) => Gpu("NVIDIA", $"GeForce RTX {m} Laptop GPU", $"RTX {m}", delta, vram);

        l.Add(Laptop("ASUS", "ROG Strix G16", 16, 240, 1499, "IPS", [i7hx, i9hx], [Rtx("4060", 0, 8), Rtx("4070", 250, 8), Rtx("4080", 900, 12)], [Ram(16, 0), Ram(32, 120)], [Storage(1000, 0), Storage(2000, 140)], 4));
        l.Add(Laptop("ASUS", "TUF Gaming A15", 15.6m, 144, 899, "IPS", [Cpu("AMD", "AMD Ryzen 7 7735HS", "Ryzen 7 7735HS", 0)], [Rtx("4050", 0, 6), Rtx("4060", 150, 8)], [Ram(16, 0)], [Storage(512, 0), Storage(1000, 70)]));
        l.Add(Laptop("ASUS", "Zenbook 14 OLED", 14, 120, 1099, "OLED", [ultra7, ultra9], [arc], [Ram(16, 0), Ram(32, 150)], [Storage(1000, 0)]));
        l.Add(Laptop("ASUS", "ROG Zephyrus G14", 14, 120, 1599, "OLED", [r9], [Rtx("4060", 0, 8), Rtx("4070", 300, 8)], [Ram(16, 0), Ram(32, 150)], [Storage(1000, 0)]));
        l.Add(Laptop("MSI", "Katana 15", 15.6m, 144, 999, "IPS", [Cpu("Intel", "Intel Core i7-13620H", "i7-13620H", 0)], [Rtx("4050", 0, 6), Rtx("4060", 120, 8), Rtx("4070", 330, 8)], [Ram(16, 0)], [Storage(1000, 0)]));
        l.Add(Laptop("MSI", "Raider GE78 HX", 17, 240, 2899, "IPS", [i9hx], [Rtx("4080", 0, 12), Rtx("4090", 700, 16)], [Ram(32, 0), Ram(64, 250)], [Storage(2000, 0)]));
        l.Add(Laptop("MSI", "Prestige 14 AI Evo", 14, 60, 1199, "IPS", [ultra7], [arc], [Ram(16, 0), Ram(32, 140)], [Storage(1000, 0)], 2));
        l.Add(Laptop("Gigabyte", "AORUS 16X", 16, 165, 1599, "IPS", [i7hx, i9hx], [Rtx("4060", 0, 8), Rtx("4070", 250, 8)], [Ram(16, 0), Ram(32, 110)], [Storage(1000, 0)]));
        l.Add(Laptop("Dell", "XPS 14", 14.5m, 120, 1699, "OLED", [ultra7, ultra9], [arc, Rtx("4050", 400, 6)], [Ram(16, 0), Ram(32, 200)], [Storage(512, 0), Storage(1000, 150)]));
        l.Add(Laptop("Alienware", "m16 R2", 16, 240, 1749, "IPS", [ultra7, ultra9], [Rtx("4060", 0, 8), Rtx("4070", 300, 8)], [Ram(16, 0), Ram(32, 150)], [Storage(1000, 0)]));
        l.Add(Laptop("Dell", "Inspiron 15", 15.6m, 120, 649, "IPS", [Cpu("Intel", "Intel Core i5-1335U", "i5-1335U", 0), Cpu("Intel", "Intel Core i7-1355U", "i7-1355U", 120)], [Gpu("Intel", "Intel Iris Xe Graphics", "", 0)], [Ram(8, 0), Ram(16, 80)], [Storage(512, 0)]));
        l.Add(Laptop("HP", "OMEN 16", 16.1m, 165, 1349, "IPS", [Cpu("Intel", "Intel Core i7-14700HX", "i7-14700HX", 0)], [Rtx("4060", 0, 8), Rtx("4070", 250, 8)], [Ram(16, 0), Ram(32, 130)], [Storage(1000, 0)]));
        l.Add(Laptop("HP", "Spectre x360 14", 14, 120, 1449, "OLED", [ultra7], [arc], [Ram(16, 0), Ram(32, 170)], [Storage(1000, 0), Storage(2000, 200)], 2));
        l.Add(Laptop("HP", "Victus 15", 15.6m, 144, 749, "IPS", [Cpu("AMD", "AMD Ryzen 5 7535HS", "Ryzen 5 7535HS", 0)], [Rtx("4050", 0, 6)], [Ram(8, 0), Ram(16, 60)], [Storage(512, 0)], 2));
        l.Add(Laptop("Lenovo", "Legion Pro 7i Gen 9", 16, 240, 2449, "IPS", [i9hx], [Rtx("4080", 0, 12), Rtx("4090", 650, 16)], [Ram(32, 0)], [Storage(1000, 0), Storage(2000, 150)]));
        l.Add(Laptop("Lenovo", "Legion Slim 5 Gen 9", 16, 165, 1299, "OLED", [r7], [Rtx("4060", 0, 8), Rtx("4070", 230, 8)], [Ram(16, 0), Ram(32, 120)], [Storage(1000, 0)]));
        l.Add(Laptop("Lenovo", "ThinkPad X1 Carbon Gen 12", 14, 60, 1899, "IPS", [Cpu("Intel", "Intel Core Ultra 7 155U", "Core Ultra 7 155U", 0)], [arc], [Ram(16, 0), Ram(32, 220)], [Storage(512, 0), Storage(1000, 160)]));
        l.Add(Laptop("Lenovo", "IdeaPad Slim 5", 14, 60, 699, "OLED", [r7], [radeon], [Ram(16, 0)], [Storage(512, 0), Storage(1000, 80)], 2));
        l.Add(Laptop("Apple", "MacBook Air 13 (M3)", 13.6m, 60, 1099, "Liquid Retina", [Cpu("Apple", "Apple M3", "M3", 0)], [Gpu("Apple", "Apple M3 10-core GPU", "", 0)], [Ram(16, 0), Ram(24, 200)], [Storage(256, 0), Storage(512, 200), Storage(1000, 400)], 4));
        l.Add(Laptop("Apple", "MacBook Pro 14 (M4 Pro)", 14.2m, 120, 1999, "Liquid Retina XDR", [Cpu("Apple", "Apple M4 Pro", "M4 Pro", 0)], [Gpu("Apple", "Apple M4 Pro 16-core GPU", "", 0)], [Ram(24, 0), Ram(48, 400)], [Storage(512, 0), Storage(1000, 200)], 3));
        l.Add(Laptop("Samsung", "Galaxy Book4 Pro 14", 14, 120, 1449, "AMOLED", [ultra7], [arc], [Ram(16, 0)], [Storage(512, 0), Storage(1000, 100)], 2));
        l.Add(Laptop("Razer", "Blade 16", 16, 240, 2999, "OLED", [i9hx], [Rtx("4080", 0, 12), Rtx("4090", 800, 16)], [Ram(32, 0)], [Storage(1000, 0), Storage(2000, 200)]));
    }

    private static void Tablets(List<ProductLine> l)
    {
        ProductLine Tab(string brand, string model, decimal screen, int hz, decimal price, string chip, int ram,
            VariantOption[] storage, VariantOption[] conn, VariantOption[] colors, int curated = 3) =>
            new("tablets", brand, model, price, d =>
            {
                d.ScreenSizeInches = screen; d.RefreshRateHz = hz; d.RamGb = ram;
                d.CpuBrand = brand == "Apple" ? "Apple" : chip.Split(' ')[0]; d.CpuModel = chip;
                d.Highlights.Add($"{screen}\" display, {hz}Hz");
            }, [storage, conn, colors], curated);

        var wifi = Conn("Wi-Fi", "Wi-Fi", 0);
        var cell = Conn("Wi-Fi + Cellular", "5G", 200);
        l.Add(Tab("Apple", "iPad Pro 13 (M4)", 13, 120, 1299, "Apple M4", 8, [Storage(256, 0, "Flash"), Storage(512, 200, "Flash"), Storage(1000, 600, "Flash")], [wifi, cell], [Color("Space Black"), Color("Silver")], 4));
        l.Add(Tab("Apple", "iPad Air 11 (M2)", 11, 60, 599, "Apple M2", 8, [Storage(128, 0, "Flash"), Storage(256, 100, "Flash")], [wifi, cell], [Color("Blue"), Color("Starlight"), Color("Purple")]));
        l.Add(Tab("Apple", "iPad (10th generation)", 10.9m, 60, 349, "Apple A14 Bionic", 4, [Storage(64, 0, "Flash"), Storage(256, 150, "Flash")], [wifi, cell], [Color("Blue"), Color("Pink"), Color("Silver")]));
        l.Add(Tab("Samsung", "Galaxy Tab S10 Ultra", 14.6m, 120, 1199, "MediaTek Dimensity 9300+", 12, [Storage(256, 0, "Flash"), Storage(512, 120, "Flash")], [wifi], [Color("Moonstone Gray"), Color("Platinum Silver")]));
        l.Add(Tab("Samsung", "Galaxy Tab S9 FE", 10.9m, 90, 449, "Samsung Exynos 1380", 6, [Storage(128, 0, "Flash"), Storage(256, 60, "Flash")], [wifi, Conn("5G", "5G", 100)], [Color("Gray"), Color("Mint")]));
        l.Add(Tab("Lenovo", "Tab P12", 12.7m, 60, 349, "MediaTek Dimensity 7050", 8, [Storage(128, 0, "Flash")], [wifi], [Color("Storm Grey", inName: false)], 1));
        l.Add(Tab("Xiaomi", "Pad 6", 11, 144, 399, "Qualcomm Snapdragon 870", 8, [Storage(128, 0, "Flash"), Storage(256, 50, "Flash")], [wifi], [Color("Gravity Gray"), Color("Mist Blue")], 2));
    }

    private static void Smartphones(List<ProductLine> l)
    {
        ProductLine Phone(string brand, string model, decimal screen, int hz, decimal price, string chip, int ram,
            VariantOption[] storage, VariantOption[] colors, int curated = 3) =>
            new("smartphones", brand, model, price, d =>
            {
                d.ScreenSizeInches = screen; d.RefreshRateHz = hz; d.RamGb = ram; d.Connectivity = "5G";
                d.CpuBrand = chip.Split(' ')[0]; d.CpuModel = chip;
                d.Attributes["Display"] = $"{screen}\" {hz}Hz OLED";
                d.Highlights.Add($"{hz}Hz OLED display");
            }, [storage, colors], curated);

        VariantOption S(int gb, decimal delta) => Storage(gb, delta, "Flash");
        l.Add(Phone("Apple", "iPhone 16 Pro Max", 6.9m, 120, 1199, "Apple A18 Pro", 8, [S(256, 0), S(512, 200), S(1000, 400)], [Color("Desert Titanium"), Color("Black Titanium"), Color("Natural Titanium")], 4));
        l.Add(Phone("Apple", "iPhone 16 Pro", 6.3m, 120, 999, "Apple A18 Pro", 8, [S(128, 0), S(256, 100), S(512, 300)], [Color("Black Titanium"), Color("White Titanium")], 3));
        l.Add(Phone("Apple", "iPhone 16", 6.1m, 60, 799, "Apple A18", 8, [S(128, 0), S(256, 100)], [Color("Ultramarine"), Color("Teal"), Color("Black")], 3));
        l.Add(Phone("Samsung", "Galaxy S25 Ultra", 6.9m, 120, 1299, "Qualcomm Snapdragon 8 Elite", 12, [S(256, 0), S(512, 120), S(1000, 360)], [Color("Titanium Silverblue"), Color("Titanium Black")], 3));
        l.Add(Phone("Samsung", "Galaxy S24", 6.2m, 120, 799, "Samsung Exynos 2400", 8, [S(128, 0), S(256, 60)], [Color("Onyx Black"), Color("Cobalt Violet")], 2));
        l.Add(Phone("Samsung", "Galaxy A55 5G", 6.6m, 120, 449, "Samsung Exynos 1480", 8, [S(128, 0), S(256, 50)], [Color("Awesome Navy"), Color("Awesome Iceblue")], 2));
        l.Add(Phone("Google", "Pixel 9 Pro", 6.3m, 120, 999, "Google Tensor G4", 16, [S(128, 0), S(256, 100)], [Color("Obsidian"), Color("Porcelain")], 2));
        l.Add(Phone("Google", "Pixel 9a", 6.3m, 120, 499, "Google Tensor G4", 8, [S(128, 0)], [Color("Obsidian"), Color("Iris")], 2));
        l.Add(Phone("Xiaomi", "14T Pro", 6.67m, 144, 799, "MediaTek Dimensity 9300+", 12, [S(256, 0), S(512, 80)], [Color("Titan Gray"), Color("Titan Blue")], 2));
        l.Add(Phone("ASUS", "ROG Phone 9 Pro", 6.78m, 185, 1199, "Qualcomm Snapdragon 8 Elite", 16, [S(512, 0)], [Color("Phantom Black")], 1));
        l.Add(Phone("ASUS", "Zenfone 11 Ultra", 6.78m, 144, 899, "Qualcomm Snapdragon 8 Gen 3", 12, [S(256, 0), S(512, 100)], [Color("Eternal Black"), Color("Skyline Blue")], 2));
    }

    private static void GraphicsCards(List<ProductLine> l)
    {
        VariantOption G(string chip, decimal msrp, int vram, string mem = "GDDR6X")
        {
            var amd = chip.StartsWith("RX", StringComparison.Ordinal);
            var family = amd ? "Radeon" : "GeForce";
            return new($"{chip} {vram}GB", msrp, d =>
            {
                d.GpuBrand = amd ? "AMD" : "NVIDIA";
                d.GpuModel = $"{family} {chip}";
                d.Attributes["VRAM"] = $"{vram} GB {mem}";
                d.Attributes["Memory type"] = mem;
                d.Highlights.Add($"{vram}GB {mem} memory");
            });
        }

        ProductLine Card(string brand, string format, decimal premium, VariantOption[] chips, string cooler, int curated = 3) =>
            new("graphics-cards", brand, format, premium, d =>
            {
                d.Attributes["Cooling"] = cooler; d.Attributes["Interface"] = "PCIe 4.0 x16";
                d.Highlights.Add(cooler);
            }, [chips], curated);

        var nv40 = new[] { G("RTX 4060", 299, 8, "GDDR6"), G("RTX 4060 Ti", 399, 8, "GDDR6"), G("RTX 4070", 549, 12, "GDDR6X"), G("RTX 4070 SUPER", 599, 12), G("RTX 4070 Ti SUPER", 799, 16), G("RTX 4080 SUPER", 999, 16), G("RTX 4090", 1599, 24) };
        var nv50 = new[] { G("RTX 5060 Ti", 429, 16, "GDDR7"), G("RTX 5070", 549, 12, "GDDR7"), G("RTX 5070 Ti", 749, 16, "GDDR7"), G("RTX 5080", 999, 16, "GDDR7"), G("RTX 5090", 1999, 32, "GDDR7") };
        var amd = new[] { G("RX 7600", 269, 8, "GDDR6"), G("RX 7700 XT", 419, 12, "GDDR6"), G("RX 7800 XT", 499, 16, "GDDR6"), G("RX 7900 GRE", 549, 16, "GDDR6"), G("RX 7900 XTX", 949, 24, "GDDR6"), G("RX 9070 XT", 599, 16, "GDDR6") };

        l.Add(Card("ASUS", "ROG Strix GeForce {0} OC", 120, nv40, "Triple-fan axial-tech cooler", 4));
        l.Add(Card("ASUS", "TUF Gaming GeForce {0} OC", 50, nv40.Concat(nv50).ToArray(), "Military-grade triple-fan cooler", 4));
        l.Add(Card("ASUS", "Dual GeForce {0} OC", 10, nv40[..3], "Dual-fan cooler", 2));
        l.Add(Card("MSI", "GeForce {0} Gaming X Trio", 70, nv40.Concat(nv50).ToArray(), "TRI FROZR 3 triple-fan cooler", 4));
        l.Add(Card("MSI", "GeForce {0} Ventus 2X OC", 0, nv40[..4], "Dual-fan cooler", 3));
        l.Add(Card("MSI", "Radeon {0} Gaming Trio", 40, amd, "TRI FROZR 3 triple-fan cooler", 3));
        l.Add(Card("Gigabyte", "GeForce {0} Gaming OC", 40, nv40.Concat(nv50).ToArray(), "WINDFORCE triple-fan cooler", 4));
        l.Add(Card("Gigabyte", "AORUS GeForce {0} Master", 200, nv40[4..].Concat(nv50[2..]).ToArray(), "Quad-slot vapor chamber cooler", 3));
        l.Add(Card("Gigabyte", "Radeon {0} Gaming OC", 30, amd, "WINDFORCE triple-fan cooler", 3));
        l.Add(Card("NVIDIA", "GeForce {0} Founders Edition", 0, nv50[1..], "Double flow-through cooler", 3));
        l.Add(Card("AMD", "Radeon {0}", 0, amd[2..5], "Reference triple-fan cooler", 2));
        l.Add(Card("ASUS", "TUF Gaming Radeon {0} OC", 50, amd, "Triple-fan axial-tech cooler", 3));
    }

    private static void Processors(List<ProductLine> l)
    {
        ProductLine Chip(string brand, string model, decimal price, int cores, int threads, string boost, string socket, int tdp, int curated = 1, bool cooler = false) =>
            new("processors", brand, model, price, d =>
            {
                d.CpuBrand = brand; d.CpuModel = $"{brand} {model}";
                d.Attributes["Cores"] = cores.ToString(); d.Attributes["Threads"] = threads.ToString();
                d.Attributes["Boost clock"] = boost; d.Attributes["Socket"] = socket; d.Attributes["TDP"] = $"{tdp} W";
                d.Attributes["Cooler included"] = cooler ? "Yes" : "No";
                d.Highlights.Add($"{cores} cores / {threads} threads up to {boost}");
            }, [None], curated);

        l.Add(Chip("Intel", "Core i9-14900K", 549, 24, 32, "6.0 GHz", "LGA1700", 125));
        l.Add(Chip("Intel", "Core i7-14700K", 399, 20, 28, "5.6 GHz", "LGA1700", 125));
        l.Add(Chip("Intel", "Core i5-14600K", 299, 14, 20, "5.3 GHz", "LGA1700", 125));
        l.Add(Chip("Intel", "Core i5-14400F", 199, 10, 16, "4.7 GHz", "LGA1700", 65, cooler: true));
        l.Add(Chip("Intel", "Core i5-12400F", 119, 6, 12, "4.4 GHz", "LGA1700", 65, cooler: true));
        l.Add(Chip("Intel", "Core Ultra 9 285K", 589, 24, 24, "5.7 GHz", "LGA1851", 125));
        l.Add(Chip("Intel", "Core Ultra 7 265K", 394, 20, 20, "5.5 GHz", "LGA1851", 125));
        l.Add(Chip("Intel", "Core Ultra 5 245K", 309, 14, 14, "5.2 GHz", "LGA1851", 125));
        l.Add(Chip("AMD", "Ryzen 7 9800X3D", 479, 8, 16, "5.2 GHz", "AM5", 120));
        l.Add(Chip("AMD", "Ryzen 7 7800X3D", 399, 8, 16, "5.0 GHz", "AM5", 120));
        l.Add(Chip("AMD", "Ryzen 9 9950X", 649, 16, 32, "5.7 GHz", "AM5", 170));
        l.Add(Chip("AMD", "Ryzen 9 9900X", 499, 12, 24, "5.6 GHz", "AM5", 120));
        l.Add(Chip("AMD", "Ryzen 5 9600X", 279, 6, 12, "5.4 GHz", "AM5", 65));
        l.Add(Chip("AMD", "Ryzen 5 7600X", 229, 6, 12, "5.3 GHz", "AM5", 105));
        l.Add(Chip("AMD", "Ryzen 7 5700X3D", 249, 8, 16, "4.1 GHz", "AM4", 105));
        l.Add(Chip("AMD", "Ryzen 5 5600", 129, 6, 12, "4.4 GHz", "AM4", 65, cooler: true));
    }

    private static void Motherboards(List<ProductLine> l)
    {
        ProductLine Board(string brand, string model, decimal price, string socket, string chipset, string form, string mem, bool wifi) =>
            new("motherboards", brand, model, price, d =>
            {
                d.Attributes["Socket"] = socket; d.Attributes["Chipset"] = chipset; d.Attributes["Form factor"] = form;
                d.Attributes["Memory type"] = mem;
                d.Connectivity = wifi ? "Wi-Fi 7" : "Ethernet";
                d.CpuBrand = socket.StartsWith("AM", StringComparison.Ordinal) ? "AMD" : "Intel";
                d.Highlights.Add($"{chipset} chipset, {form}, {mem}");
            }, [None], 1);

        l.Add(Board("ASUS", "ROG Strix B650-A Gaming WiFi", 229, "AM5", "AMD B650", "ATX", "DDR5", true));
        l.Add(Board("ASUS", "TUF Gaming X670E-Plus WiFi", 269, "AM5", "AMD X670E", "ATX", "DDR5", true));
        l.Add(Board("ASUS", "ROG Strix X870E-E Gaming WiFi", 499, "AM5", "AMD X870E", "ATX", "DDR5", true));
        l.Add(Board("ASUS", "PRIME B760M-A WIFI D4", 139, "LGA1700", "Intel B760", "Micro-ATX", "DDR4", true));
        l.Add(Board("ASUS", "ROG Maximus Z790 Hero", 599, "LGA1700", "Intel Z790", "ATX", "DDR5", true));
        l.Add(Board("ASUS", "ROG Strix Z890-E Gaming WiFi", 499, "LGA1851", "Intel Z890", "ATX", "DDR5", true));
        l.Add(Board("MSI", "MAG B650 Tomahawk WiFi", 219, "AM5", "AMD B650", "ATX", "DDR5", true));
        l.Add(Board("MSI", "MAG X870 Tomahawk WiFi", 299, "AM5", "AMD X870", "ATX", "DDR5", true));
        l.Add(Board("MSI", "MPG Z790 Edge WiFi", 349, "LGA1700", "Intel Z790", "ATX", "DDR5", true));
        l.Add(Board("MSI", "PRO B760M-P DDR4", 109, "LGA1700", "Intel B760", "Micro-ATX", "DDR4", false));
        l.Add(Board("MSI", "MAG B550 Tomahawk", 159, "AM4", "AMD B550", "ATX", "DDR4", false));
        l.Add(Board("Gigabyte", "B650 AORUS Elite AX", 209, "AM5", "AMD B650", "ATX", "DDR5", true));
        l.Add(Board("Gigabyte", "X870E AORUS Master", 499, "AM5", "AMD X870E", "E-ATX", "DDR5", true));
        l.Add(Board("Gigabyte", "Z790 AORUS Elite AX", 259, "LGA1700", "Intel Z790", "ATX", "DDR5", true));
        l.Add(Board("Gigabyte", "B760M DS3H DDR4", 104, "LGA1700", "Intel B760", "Micro-ATX", "DDR4", false));
        l.Add(Board("Gigabyte", "B650I AORUS Ultra", 279, "AM5", "AMD B650", "Mini-ITX", "DDR5", true));
    }

    private static void Memory(List<ProductLine> l)
    {
        VariantOption Kit(int total, int sticks, string speed, string latency, decimal price) =>
            new($"{total}GB ({sticks}x{total / sticks}GB) {speed} {latency}", price, d =>
            {
                d.RamGb = total; d.Attributes["Speed"] = speed; d.Attributes["CAS latency"] = latency;
                d.Attributes["Modules"] = $"{sticks} x {total / sticks} GB";
            });

        ProductLine Ram(string brand, string model, string type, decimal premium, VariantOption[] kits, VariantOption[] colors, int curated = 3, bool rgb = false) =>
            new("ram", brand, model, premium, d =>
            {
                d.Attributes["Memory type"] = type; d.Attributes["RGB"] = rgb ? "Yes" : "No";
                d.Highlights.Add(rgb ? "addressable RGB lighting" : "low-profile heat spreader");
            }, [kits, colors], curated);

        var black = Color("Black", inName: false);
        var white = Color("White");
        var ddr5 = new[] { Kit(32, 2, "DDR5-6000", "CL30", 109), Kit(64, 2, "DDR5-6000", "CL30", 199), Kit(32, 2, "DDR5-5600", "CL36", 89), Kit(96, 2, "DDR5-6400", "CL32", 289) };
        var ddr4 = new[] { Kit(16, 2, "DDR4-3200", "CL16", 39), Kit(32, 2, "DDR4-3200", "CL16", 64), Kit(32, 2, "DDR4-3600", "CL18", 72) };

        l.Add(Ram("Corsair", "Vengeance DDR5", "DDR5", 0, ddr5, [black, white], 4));
        l.Add(Ram("Corsair", "Vengeance RGB DDR5", "DDR5", 20, ddr5, [black, white], 3, rgb: true));
        l.Add(Ram("Corsair", "Dominator Titanium RGB DDR5", "DDR5", 80, ddr5[..2], [black, white], 2, rgb: true));
        l.Add(Ram("Corsair", "Vengeance LPX DDR4", "DDR4", 0, ddr4, [black], 2));
        l.Add(Ram("Kingston", "FURY Beast DDR5", "DDR5", 0, ddr5, [black, white], 3));
        l.Add(Ram("Kingston", "FURY Renegade RGB DDR5", "DDR5", 40, ddr5[..2], [black, white], 2, rgb: true));
        l.Add(Ram("Kingston", "FURY Beast DDR4", "DDR4", 0, ddr4, [black], 2));
        l.Add(Ram("G.Skill", "Trident Z5 RGB DDR5", "DDR5", 25, ddr5, [black], 2, rgb: true));
        l.Add(Ram("Crucial", "Pro DDR5", "DDR5", -10, ddr5[..3], [black], 2));
    }

    private static void Ssds(List<ProductLine> l)
    {
        ProductLine Ssd(string brand, string model, string type, string iface, string read, VariantOption[] caps, int curated = 3) =>
            new("ssds", brand, model, 0, d =>
            {
                d.StorageType = type; d.Attributes["Interface"] = iface; d.Attributes["Sequential read"] = read;
                d.Attributes["Form factor"] = type == "NVMe" ? "M.2 2280" : "2.5\"";
                d.Highlights.Add($"up to {read} sequential reads over {iface}");
            }, [caps], curated);

        VariantOption C(int gb, decimal price, string suffix) =>
            new($"{Units.StorageLabel(gb)} {suffix}", price, d => d.StorageGb = gb);

        l.Add(Ssd("Samsung", "990 PRO", "NVMe", "PCIe 4.0 x4", "7,450 MB/s", [C(1000, 119, "NVMe M.2 SSD"), C(2000, 179, "NVMe M.2 SSD"), C(4000, 319, "NVMe M.2 SSD")], 3));
        l.Add(Ssd("Samsung", "990 EVO Plus", "NVMe", "PCIe 4.0 x4 / 5.0 x2", "7,250 MB/s", [C(1000, 89, "NVMe M.2 SSD"), C(2000, 139, "NVMe M.2 SSD"), C(4000, 259, "NVMe M.2 SSD")], 2));
        l.Add(Ssd("Samsung", "870 EVO", "SATA", "SATA III 6Gb/s", "560 MB/s", [C(500, 54, "SATA 2.5\" SSD"), C(1000, 89, "SATA 2.5\" SSD"), C(2000, 159, "SATA 2.5\" SSD")], 3));
        l.Add(Ssd("Western Digital", "WD_BLACK SN850X", "NVMe", "PCIe 4.0 x4", "7,300 MB/s", [C(1000, 99, "NVMe SSD"), C(2000, 159, "NVMe SSD"), C(4000, 299, "NVMe SSD")], 3));
        l.Add(Ssd("Western Digital", "WD Blue SN580", "NVMe", "PCIe 4.0 x4", "4,150 MB/s", [C(500, 44, "NVMe SSD"), C(1000, 64, "NVMe SSD"), C(2000, 119, "NVMe SSD")], 2));
        l.Add(Ssd("Kingston", "KC3000", "NVMe", "PCIe 4.0 x4", "7,000 MB/s", [C(1000, 89, "NVMe M.2 SSD"), C(2000, 149, "NVMe M.2 SSD")], 2));
        l.Add(Ssd("Kingston", "NV3", "NVMe", "PCIe 4.0 x4", "6,000 MB/s", [C(500, 39, "NVMe M.2 SSD"), C(1000, 59, "NVMe M.2 SSD"), C(2000, 109, "NVMe M.2 SSD")], 2));
        l.Add(Ssd("Kingston", "A400", "SATA", "SATA III 6Gb/s", "500 MB/s", [C(480, 32, "SATA 2.5\" SSD"), C(960, 55, "SATA 2.5\" SSD")], 2));
        l.Add(Ssd("Crucial", "T705", "NVMe", "PCIe 5.0 x4", "14,500 MB/s", [C(1000, 179, "PCIe Gen5 NVMe SSD"), C(2000, 299, "PCIe Gen5 NVMe SSD")], 2));
        l.Add(Ssd("Crucial", "P3 Plus", "NVMe", "PCIe 4.0 x4", "5,000 MB/s", [C(1000, 59, "NVMe M.2 SSD"), C(2000, 109, "NVMe M.2 SSD")], 2));
        l.Add(Ssd("Seagate", "FireCuda 530R", "NVMe", "PCIe 4.0 x4", "7,300 MB/s", [C(1000, 109, "NVMe SSD"), C(2000, 179, "NVMe SSD")], 2));
        l.Add(Ssd("Corsair", "MP600 PRO LPX", "NVMe", "PCIe 4.0 x4", "7,100 MB/s", [C(1000, 89, "NVMe M.2 SSD"), C(2000, 149, "NVMe M.2 SSD")], 2));
    }

    private static void Hdds(List<ProductLine> l)
    {
        ProductLine Hdd(string brand, string model, string rpm, string cache, string use, VariantOption[] caps, int curated = 3) =>
            new("hdds", brand, model, 0, d =>
            {
                d.StorageType = "HDD"; d.Attributes["Spindle speed"] = rpm; d.Attributes["Cache"] = cache;
                d.Attributes["Workload"] = use; d.Attributes["Form factor"] = "3.5\"";
                d.Highlights.Add($"{rpm} drive built for {use.ToLowerInvariant()}");
            }, [caps], curated);

        VariantOption C(int gb, decimal price, string suffix) =>
            new($"{Units.StorageLabel(gb)} {suffix}", price, d => d.StorageGb = gb);

        l.Add(Hdd("Seagate", "BarraCuda", "7200 RPM", "256 MB", "Desktop", [C(2000, 64, "Internal Hard Drive"), C(4000, 84, "Internal Hard Drive"), C(8000, 149, "Internal Hard Drive")]));
        l.Add(Hdd("Seagate", "IronWolf", "5400 RPM", "256 MB", "NAS", [C(4000, 99, "NAS Hard Drive"), C(8000, 179, "NAS Hard Drive"), C(12000, 239, "NAS Hard Drive")]));
        l.Add(Hdd("Seagate", "Exos X20", "7200 RPM", "256 MB", "Enterprise", [C(18000, 299, "Enterprise HDD"), C(20000, 339, "Enterprise HDD")], 2));
        l.Add(Hdd("Western Digital", "WD Blue", "5400 RPM", "256 MB", "Desktop", [C(1000, 49, "Desktop Hard Drive"), C(2000, 59, "Desktop Hard Drive"), C(4000, 79, "Desktop Hard Drive")]));
        l.Add(Hdd("Western Digital", "WD Red Plus", "5400 RPM", "256 MB", "NAS", [C(4000, 99, "NAS Hard Drive"), C(8000, 189, "NAS Hard Drive"), C(12000, 259, "NAS Hard Drive")]));
        l.Add(Hdd("Western Digital", "WD Purple", "5400 RPM", "256 MB", "Surveillance", [C(4000, 94, "Surveillance Hard Drive"), C(8000, 169, "Surveillance Hard Drive")], 2));
    }

    private static void PowerSupplies(List<ProductLine> l)
    {
        ProductLine Psu(string brand, string model, int watts, string rating, string modular, decimal price, string atx = "ATX 3.1") =>
            new("power-supplies", brand, model, price, d =>
            {
                d.Attributes["Wattage"] = $"{watts} W"; d.Attributes["Efficiency"] = $"80 PLUS {rating}";
                d.Attributes["Modular"] = modular; d.Attributes["Standard"] = atx;
                d.Highlights.Add($"{watts}W 80 PLUS {rating}, {modular.ToLowerInvariant()}");
            }, [None], 1);

        l.Add(Psu("Corsair", "RM850x (2024) 850W 80 PLUS Gold Fully Modular", 850, "Gold", "Fully modular", 149));
        l.Add(Psu("Corsair", "RM1000x (2024) 1000W 80 PLUS Gold Fully Modular", 1000, "Gold", "Fully modular", 189));
        l.Add(Psu("Corsair", "HX1500i 1500W 80 PLUS Platinum", 1500, "Platinum", "Fully modular", 349));
        l.Add(Psu("Corsair", "CX650M 650W 80 PLUS Bronze", 650, "Bronze", "Semi-modular", 74, "ATX 2.4"));
        l.Add(Psu("Seasonic", "FOCUS GX-750 750W 80 PLUS Gold", 750, "Gold", "Fully modular", 119));
        l.Add(Psu("Seasonic", "PRIME TX-1000 1000W 80 PLUS Titanium", 1000, "Titanium", "Fully modular", 329));
        l.Add(Psu("MSI", "MAG A850GL PCIE5 850W 80 PLUS Gold", 850, "Gold", "Fully modular", 119));
        l.Add(Psu("MSI", "MPG A1000G PCIE5 1000W 80 PLUS Gold", 1000, "Gold", "Fully modular", 179));
        l.Add(Psu("ASUS", "ROG Thor 1000W Platinum II", 1000, "Platinum", "Fully modular", 329));
        l.Add(Psu("ASUS", "TUF Gaming 750W Gold", 750, "Gold", "Non-modular", 99));
        l.Add(Psu("Gigabyte", "UD850GM PG5 850W 80 PLUS Gold", 850, "Gold", "Fully modular", 109));
        l.Add(Psu("be quiet!", "Straight Power 12 1000W 80 PLUS Platinum", 1000, "Platinum", "Fully modular", 229));
    }

    private static void Cases(List<ProductLine> l)
    {
        ProductLine Case(string brand, string model, string form, decimal price, string fans, int curated = 2) =>
            new("cases", brand, model, price, d =>
            {
                d.Attributes["Form factor"] = form; d.Attributes["Included fans"] = fans;
                d.Attributes["Side panel"] = "Tempered glass";
                d.Highlights.Add($"{form} case with {fans.ToLowerInvariant()}");
            }, [[Color("Black"), Color("White", 10)]], curated);

        l.Add(Case("Lian Li", "O11 Dynamic EVO", "Mid Tower", 159, "No fans included"));
        l.Add(Case("Lian Li", "Lancool 216", "Mid Tower", 99, "2 x 160mm ARGB + 1 x 140mm"));
        l.Add(Case("NZXT", "H6 Flow", "Mid Tower", 109, "3 x 120mm"));
        l.Add(Case("NZXT", "H9 Flow", "Mid Tower", 159, "4 x 120mm"));
        l.Add(Case("Corsair", "4000D Airflow", "Mid Tower", 104, "2 x 120mm"));
        l.Add(Case("Corsair", "5000D Airflow", "Mid Tower", 174, "2 x 120mm"));
        l.Add(Case("Corsair", "6500X", "Mid Tower", 199, "No fans included"));
        l.Add(Case("Fractal Design", "North", "Mid Tower", 139, "2 x 140mm"));
        l.Add(Case("Fractal Design", "Meshify 2 Compact", "Mid Tower", 119, "3 x 140mm"));
        l.Add(Case("ASUS", "ROG Hyperion GR701", "Full Tower", 349, "4 x 140mm"));
        l.Add(Case("ASUS", "TUF Gaming GT502", "Mid Tower", 159, "No fans included"));
        l.Add(Case("MSI", "MAG Forge 120A Airflow", "Mid Tower", 69, "6 x 120mm ARGB", 1));
        l.Add(Case("be quiet!", "Pure Base 500DX", "Mid Tower", 109, "3 x 140mm"));
    }

    private static void Monitors(List<ProductLine> l)
    {
        ProductLine Mon(string brand, string model, decimal size, int hz, string res, string panel, decimal price, string? conn = "DisplayPort 1.4, HDMI 2.1") =>
            new("monitors", brand, model, price, d =>
            {
                d.ScreenSizeInches = size; d.RefreshRateHz = hz; d.Connectivity = conn;
                d.Attributes["Resolution"] = res; d.Attributes["Panel"] = panel;
                d.Highlights.Add($"{size}\" {res} {panel} at {hz}Hz");
            }, [None], 1);

        l.Add(Mon("LG", "UltraGear 27GR95QE-B 27\" QHD OLED 240Hz Gaming Monitor", 27, 240, "2560x1440", "OLED", 799));
        l.Add(Mon("LG", "UltraGear 32GS95UE-B 32\" 4K OLED 240Hz Gaming Monitor", 32, 240, "3840x2160", "OLED", 1399));
        l.Add(Mon("LG", "27UP850N-W 27\" 4K UHD IPS Monitor", 27, 60, "3840x2160", "IPS", 379, "USB-C 96W, DisplayPort, HDMI"));
        l.Add(Mon("Samsung", "Odyssey OLED G8 34\" Ultra-WQHD 175Hz", 34, 175, "3440x1440", "QD-OLED", 1099));
        l.Add(Mon("Samsung", "Odyssey G5 27\" QHD 165Hz Curved Gaming Monitor", 27, 165, "2560x1440", "VA", 279));
        l.Add(Mon("Samsung", "ViewFinity S8 27\" 4K UHD Monitor", 27, 60, "3840x2160", "IPS", 449));
        l.Add(Mon("ASUS", "ROG Swift PG27AQDM 27\" QHD OLED 240Hz", 27, 240, "2560x1440", "OLED", 899));
        l.Add(Mon("ASUS", "TUF Gaming VG27AQ1A 27\" QHD 170Hz", 27, 170, "2560x1440", "IPS", 259));
        l.Add(Mon("ASUS", "ProArt PA278CV 27\" QHD Professional Monitor", 27, 75, "2560x1440", "IPS", 329, "USB-C 65W, DisplayPort, HDMI"));
        l.Add(Mon("MSI", "MAG 274QRF QD E2 27\" QHD 180Hz", 27, 180, "2560x1440", "Rapid IPS", 299));
        l.Add(Mon("MSI", "MPG 321URX 32\" 4K QD-OLED 240Hz", 32, 240, "3840x2160", "QD-OLED", 1199));
        l.Add(Mon("Gigabyte", "M27Q 27\" QHD 170Hz KVM Gaming Monitor", 27, 170, "2560x1440", "IPS", 279));
        l.Add(Mon("Gigabyte", "AORUS FO32U2P 32\" 4K QD-OLED 240Hz", 32, 240, "3840x2160", "QD-OLED", 1299, "DisplayPort 2.1, HDMI 2.1"));
        l.Add(Mon("Dell", "UltraSharp U2724D 27\" QHD 120Hz", 27, 120, "2560x1440", "IPS Black", 459));
        l.Add(Mon("Dell", "S2722DGM 27\" QHD 165Hz Curved Gaming Monitor", 27, 165, "2560x1440", "VA", 249));
        l.Add(Mon("Alienware", "AW3423DWF 34\" QD-OLED 165Hz Curved", 34, 165, "3440x1440", "QD-OLED", 899));
        l.Add(Mon("HP", "OMEN 27qs 27\" QHD 240Hz", 27, 240, "2560x1440", "IPS", 449));
        l.Add(Mon("Apple", "Studio Display 27\" 5K Retina", 27, 60, "5120x2880", "IPS", 1599, "Thunderbolt 3"));
        l.Add(Mon("Lenovo", "Legion Y27qf-30 27\" QHD 250Hz", 27, 250, "2560x1440", "IPS", 399));
        l.Add(Mon("Samsung", "Odyssey Neo G9 57\" Dual 4K UHD 240Hz", 57, 240, "7680x2160", "Mini-LED VA", 1999, "DisplayPort 2.1, HDMI 2.1"));
    }

    private static void Keyboards(List<ProductLine> l)
    {
        ProductLine Kb(string brand, string model, decimal price, string layout, string switches, VariantOption[] conn, VariantOption[] colors, int curated = 2) =>
            new("keyboards", brand, model, price, d =>
            {
                d.Attributes["Layout"] = layout; d.Attributes["Switches"] = switches;
                d.Highlights.Add($"{layout.ToLowerInvariant()} layout with {switches}");
            }, [conn, colors], curated);

        var wired = Conn("", "Wired", 0);
        var wireless = Conn("", "Wireless", 0);
        var black = Color("Black", inName: false);
        var white = Color("White");
        l.Add(Kb("Logitech", "G915 X Lightspeed TKL Wireless Gaming Keyboard", 229, "Tenkeyless", "low-profile GL Tactile switches", [wireless], [black, white]));
        l.Add(Kb("Logitech", "G Pro X TKL Lightspeed Gaming Keyboard", 199, "Tenkeyless", "GX Brown tactile switches", [wireless], [black, white, Color("Magenta")], 3));
        l.Add(Kb("Logitech", "MX Keys S Wireless Keyboard", 109, "Full-size", "low-profile scissor keys", [Conn("", "Bluetooth", 0)], [Color("Graphite", inName: false), Color("Pale Grey")]));
        l.Add(Kb("Logitech", "K380 Multi-Device Bluetooth Keyboard", 39, "Compact", "low-profile scissor keys", [Conn("", "Bluetooth", 0)], [Color("Graphite", inName: false), Color("Rose")]));
        l.Add(Kb("Corsair", "K70 RGB PRO Mechanical Gaming Keyboard", 159, "Full-size", "Cherry MX Red switches", [wired], [black]));
        l.Add(Kb("Corsair", "K65 PLUS Wireless 75% Mechanical Keyboard", 159, "75%", "hot-swappable MLX Red switches", [wireless], [black, white]));
        l.Add(Kb("Corsair", "K100 RGB Optical-Mechanical Gaming Keyboard", 229, "Full-size", "OPX optical switches", [wired], [black], 1));
        l.Add(Kb("Razer", "BlackWidow V4 Pro Mechanical Gaming Keyboard", 229, "Full-size", "Razer Green clicky switches", [wired], [black], 1));
        l.Add(Kb("Razer", "Huntsman V3 Pro TKL Analog Optical Keyboard", 249, "Tenkeyless", "analog optical switches", [wired], [black, white]));
        l.Add(Kb("Razer", "DeathStalker V2 Pro Wireless Low-Profile Keyboard", 249, "Full-size", "low-profile optical switches", [wireless], [black, white]));
        l.Add(Kb("SteelSeries", "Apex Pro TKL (2023) Gaming Keyboard", 209, "Tenkeyless", "OmniPoint 2.0 adjustable switches", [wired, Conn("Wireless", "Wireless", 40)], [black]));
        l.Add(Kb("ASUS", "ROG Azoth 75% Wireless Gaming Keyboard", 249, "75%", "hot-swappable ROG NX switches", [wireless], [black, white]));
        l.Add(Kb("HyperX", "Alloy Origins Core TKL Mechanical Keyboard", 89, "Tenkeyless", "HyperX Red linear switches", [wired], [black], 1));
        l.Add(Kb("Apple", "Magic Keyboard with Touch ID", 149, "Full-size", "scissor mechanism", [Conn("", "Bluetooth", 0)], [white, Color("Black", 30)], 2));
    }

    private static void Mice(List<ProductLine> l)
    {
        ProductLine Mouse(string brand, string model, decimal price, string sensor, int dpi, int grams, VariantOption[] conn, VariantOption[] colors, int curated = 2) =>
            new("mice", brand, model, price, d =>
            {
                d.Attributes["Sensor"] = sensor; d.Attributes["Max DPI"] = dpi.ToString("N0");
                d.Attributes["Weight"] = $"{grams} g";
                d.Highlights.Add($"{sensor} sensor at up to {dpi:N0} DPI, {grams} g");
            }, [conn, colors], curated);

        var wired = Conn("", "Wired", 0);
        var wireless = Conn("", "Wireless", 0);
        var black = Color("Black", inName: false);
        var white = Color("White");
        l.Add(Mouse("Logitech", "G Pro X Superlight 2 Wireless Gaming Mouse", 159, "HERO 2", 32000, 60, [wireless], [black, white, Color("Magenta")], 3));
        l.Add(Mouse("Logitech", "G502 X Plus Wireless Gaming Mouse", 159, "HERO 25K", 25600, 106, [wireless], [black, white]));
        l.Add(Mouse("Logitech", "MX Master 3S Performance Wireless Mouse", 99, "Darkfield 8K", 8000, 141, [Conn("", "Bluetooth", 0)], [Color("Graphite", inName: false), Color("Pale Grey")]));
        l.Add(Mouse("Logitech", "G305 Lightspeed Wireless Gaming Mouse", 49, "HERO", 12000, 99, [wireless], [black, white, Color("Lilac")], 3));
        l.Add(Mouse("Razer", "DeathAdder V3 Pro Wireless Gaming Mouse", 149, "Focus Pro 30K", 30000, 63, [wireless], [black, white]));
        l.Add(Mouse("Razer", "Viper V3 Pro Wireless Esports Mouse", 159, "Focus Pro 35K", 35000, 54, [wireless], [black, white]));
        l.Add(Mouse("Razer", "Basilisk V3 Wired Gaming Mouse", 69, "Focus+ 26K", 26000, 101, [wired], [black], 1));
        l.Add(Mouse("Corsair", "M75 Wireless Lightweight Gaming Mouse", 129, "Marksman 26K", 26000, 89, [wireless], [black, white]));
        l.Add(Mouse("Corsair", "Dark Core RGB PRO SE Wireless Mouse", 89, "Marksman", 18000, 133, [wireless], [black], 1));
        l.Add(Mouse("SteelSeries", "Aerox 5 Wireless Gaming Mouse", 139, "TrueMove Air", 18000, 74, [wireless], [black]));
        l.Add(Mouse("ASUS", "ROG Harpe Ace Aim Lab Edition Mouse", 139, "ROG AimPoint", 36000, 54, [wireless], [black, white]));
        l.Add(Mouse("HyperX", "Pulsefire Haste 2 Gaming Mouse", 59, "HyperX 26K", 26000, 53, [wired, Conn("Wireless", "Wireless", 30)], [black, white], 3));
        l.Add(Mouse("Apple", "Magic Mouse (USB-C)", 79, "optical", 1300, 99, [Conn("", "Bluetooth", 0)], [white, Color("Black", 20)]));
    }

    private static void Headsets(List<ProductLine> l)
    {
        ProductLine Hs(string brand, string model, decimal price, string driver, VariantOption[] conn, VariantOption[] colors, int curated = 2, bool anc = false) =>
            new("headsets", brand, model, price, d =>
            {
                d.Attributes["Driver"] = driver; d.Attributes["Noise cancelling"] = anc ? "Active" : "Passive";
                d.Highlights.Add($"{driver} drivers{(anc ? " with active noise cancelling" : "")}");
            }, [conn, colors], curated);

        var wired = Conn("", "Wired", 0);
        var wireless = Conn("", "Wireless", 0);
        var black = Color("Black", inName: false);
        var white = Color("White");
        l.Add(Hs("SteelSeries", "Arctis Nova Pro Wireless Gaming Headset", 349, "40mm neodymium", [wireless], [black, white], 2, anc: true));
        l.Add(Hs("SteelSeries", "Arctis Nova 7 Wireless Gaming Headset", 179, "40mm neodymium", [wireless], [black, white]));
        l.Add(Hs("Logitech", "G Pro X 2 Lightspeed Wireless Gaming Headset", 249, "50mm graphene", [wireless], [black, white]));
        l.Add(Hs("Logitech", "G435 Lightspeed Wireless Gaming Headset", 79, "40mm", [wireless], [black, white, Color("Blue")], 3));
        l.Add(Hs("Razer", "BlackShark V2 Pro (2023) Wireless Esports Headset", 199, "50mm TriForce Titanium", [wireless], [black, white]));
        l.Add(Hs("Razer", "Kraken V4 Wireless Gaming Headset", 199, "40mm TriForce Bio-Cellulose", [wireless], [black], 1));
        l.Add(Hs("Corsair", "HS80 RGB Wireless Gaming Headset", 149, "50mm neodymium", [wireless], [black, white]));
        l.Add(Hs("Corsair", "Virtuoso RGB Wireless XT Gaming Headset", 269, "50mm high-density neodymium", [wireless], [black], 1));
        l.Add(Hs("HyperX", "Cloud III Wireless Gaming Headset", 169, "53mm angled", [wireless], [black, Color("Red")]));
        l.Add(Hs("HyperX", "Cloud Alpha Gaming Headset", 99, "50mm dual-chamber", [wired], [black, Color("Red")]));
        l.Add(Hs("ASUS", "ROG Delta II Wireless Gaming Headset", 199, "50mm titanium-plated", [wireless], [black], 1));
        l.Add(Hs("Apple", "AirPods Max (USB-C)", 549, "40mm Apple-designed dynamic", [Conn("", "Bluetooth", 0)], [Color("Midnight"), Color("Starlight"), Color("Blue")], 3, anc: true));
        l.Add(Hs("Samsung", "Galaxy Buds3 Pro", 249, "10.5mm two-way", [Conn("", "Bluetooth", 0)], [Color("Silver"), Color("White", inName: false)], 2, anc: true));
    }

    private static void Routers(List<ProductLine> l)
    {
        ProductLine Router(string brand, string model, decimal price, string wifi, string speed, string ports, int curated = 1) =>
            new("routers", brand, model, price, d =>
            {
                d.Connectivity = wifi; d.Attributes["Max speed"] = speed; d.Attributes["Ports"] = ports;
                d.Highlights.Add($"{wifi} with up to {speed}");
            }, [None], curated);

        l.Add(Router("TP-Link", "Archer AX55 AX3000 Dual-Band Wi-Fi 6 Router", 89, "Wi-Fi 6", "3 Gbps", "4 x Gigabit LAN"));
        l.Add(Router("TP-Link", "Archer AXE75 AXE5400 Tri-Band Wi-Fi 6E Router", 159, "Wi-Fi 6E", "5.4 Gbps", "4 x Gigabit LAN"));
        l.Add(Router("TP-Link", "Archer BE800 BE19000 Tri-Band Wi-Fi 7 Router", 549, "Wi-Fi 7", "19 Gbps", "2 x 10G, 4 x 2.5G"));
        l.Add(Router("TP-Link", "Deco X55 AX3000 Whole Home Mesh Wi-Fi 6 System (3-pack)", 199, "Wi-Fi 6", "3 Gbps", "3 x Gigabit per unit"));
        l.Add(Router("TP-Link", "Deco BE85 BE19000 Tri-Band Mesh Wi-Fi 7 System (2-pack)", 999, "Wi-Fi 7", "19 Gbps", "2 x 10G, 2 x 2.5G per unit"));
        l.Add(Router("ASUS", "RT-AX88U Pro AX6000 Dual-Band Wi-Fi 6 Gaming Router", 299, "Wi-Fi 6", "6 Gbps", "2 x 2.5G, 4 x Gigabit LAN"));
        l.Add(Router("ASUS", "ROG Rapture GT-BE98 Quad-Band Wi-Fi 7 Gaming Router", 699, "Wi-Fi 7", "25 Gbps", "2 x 10G, 4 x 2.5G"));
        l.Add(Router("ASUS", "ZenWiFi BT10 Tri-Band Mesh Wi-Fi 7 System (2-pack)", 699, "Wi-Fi 7", "18 Gbps", "1 x 10G, 3 x 2.5G per unit"));
        l.Add(Router("Netgear", "Nighthawk RS700S BE19000 Tri-Band Wi-Fi 7 Router", 549, "Wi-Fi 7", "19 Gbps", "1 x 10G, 4 x Gigabit LAN"));
        l.Add(Router("Netgear", "Orbi 970 Series Quad-Band Mesh Wi-Fi 7 System (3-pack)", 1999, "Wi-Fi 7", "27 Gbps", "1 x 10G, 2 x 2.5G per unit"));
    }

    private static void Switches(List<ProductLine> l)
    {
        ProductLine Sw(string brand, string model, decimal price, int ports, string speed, string mgmt, bool poe = false) =>
            new("switches", brand, model, price, d =>
            {
                d.Connectivity = "Ethernet"; d.Attributes["Ports"] = ports.ToString(); d.Attributes["Port speed"] = speed;
                d.Attributes["Management"] = mgmt; d.Attributes["PoE"] = poe ? "Yes" : "No";
                d.Highlights.Add($"{ports}-port {speed} {mgmt.ToLowerInvariant()} switch");
            }, [None], 1);

        l.Add(Sw("TP-Link", "TL-SG108 8-Port Gigabit Desktop Switch", 22, 8, "Gigabit", "Unmanaged"));
        l.Add(Sw("TP-Link", "TL-SG116 16-Port Gigabit Desktop Switch", 49, 16, "Gigabit", "Unmanaged"));
        l.Add(Sw("TP-Link", "TL-SG108E 8-Port Gigabit Easy Smart Switch", 32, 8, "Gigabit", "Smart managed"));
        l.Add(Sw("TP-Link", "TL-SG1008MP 8-Port Gigabit PoE+ Switch", 89, 8, "Gigabit", "Unmanaged", poe: true));
        l.Add(Sw("TP-Link", "TL-SX1008 8-Port 10G Desktop Switch", 399, 8, "10 Gigabit", "Unmanaged"));
        l.Add(Sw("Netgear", "GS308 8-Port Gigabit Ethernet Switch", 24, 8, "Gigabit", "Unmanaged"));
        l.Add(Sw("Netgear", "GS108PE 8-Port Gigabit PoE Plus Switch", 79, 8, "Gigabit", "Smart managed", poe: true));
        l.Add(Sw("Netgear", "MS305 5-Port 2.5G Multi-Gigabit Switch", 99, 5, "2.5 Gigabit", "Unmanaged"));
    }
}
