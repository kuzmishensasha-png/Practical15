namespace Practical1.Infra.Remote;

public sealed class AmbrSummary
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Vision { get; set; } = "";
    public string Weapon { get; set; } = "";
    public int Rarity { get; set; }
    public string IconUrl { get; set; } = "";
    public string ArtworkUrl { get; set; } = "";
    public string Region { get; set; } = "";
    public string SpecialStat { get; set; } = "";
    public string ReleaseDate { get; set; } = "";
    public bool IsBeta { get; set; }
}

public sealed class AmbrStat
{
    public string Name { get; set; } = "";
    public double BaseValue { get; set; }
    public string GrowthCurve { get; set; } = "";
}

public sealed class AmbrAscension
{
    public int Phase { get; set; }
    public int MaxLevel { get; set; }
    public int Mora { get; set; }
    public int AdventureRank { get; set; }
    public Dictionary<string, int> Materials { get; set; } = new();
    public Dictionary<string, double> BonusStats { get; set; } = new();
}

public sealed class AmbrTalent
{
    public string Type { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string IconUrl { get; set; } = "";
    public double? Cooldown { get; set; }
    public int? EnergyCost { get; set; }
}

public sealed class AmbrConstellation
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string IconUrl { get; set; } = "";
}
