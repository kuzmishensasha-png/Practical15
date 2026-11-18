namespace Practical1.CLI.Catalog;

public class CatalogCharacter
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Vision { get; set; } = "";
    public string Weapon { get; set; } = "";
    public int Rarity { get; set; }
    public string ImageUrl { get; set; } = "";
    public string ArtworkUrl { get; set; } = "";
    public string Title { get; set; } = "";
    public string Region { get; set; } = "";
    public string Affiliation { get; set; } = "";
    public string ConstellationName { get; set; } = "";
    public string Birthday { get; set; } = "";
    public string SpecialStat { get; set; } = "";
    public string ReleaseDate { get; set; } = "";
    public bool IsBeta { get; set; }
    public Dictionary<string, string> VoiceActors { get; set; } = new();
    public List<Practical1.Infra.Remote.AmbrStat> BaseStats { get; set; } = new();
    public List<Practical1.Infra.Remote.AmbrAscension> Ascensions { get; set; } = new();
    public List<Practical1.Infra.Remote.AmbrTalent> Talents { get; set; } = new();
    public List<Practical1.Infra.Remote.AmbrConstellation> Constellations { get; set; } = new();
    public Dictionary<string, int> AscensionMaterials { get; set; } = new();
    public bool IsCustom { get; set; }
    public bool DetailsLoaded { get; set; }
}

public class CatalogData
{
    public List<CatalogCharacter> Characters { get; set; } = new();
}
