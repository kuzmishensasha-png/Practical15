namespace Practical1.Infra.Remote;

public class RemoteCharacterData
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int Rarity { get; set; }
    public string Vision { get; set; } = "";
    public string Weapon { get; set; } = "";
    public string IconUrl { get; set; } = "";
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
    public List<AmbrStat> BaseStats { get; set; } = new();
    public List<AmbrAscension> Ascensions { get; set; } = new();
    public List<AmbrTalent> Talents { get; set; } = new();
    public List<AmbrConstellation> Constellations { get; set; } = new();
    public Dictionary<string, int> AscensionMaterials { get; set; } = new();
    public List<RemoteTalentData> SkillTalents { get; set; } = new();
}
