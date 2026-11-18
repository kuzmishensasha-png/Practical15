namespace Practical1.Infra.Remote;

public class RemoteWeaponData
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public int Rarity { get; set; }
    public int BaseAttack { get; set; }
    public string SubStat { get; set; } = "";
    public string PassiveName { get; set; } = "";
    public string PassiveDesc { get; set; } = "";
    public string Description { get; set; } = "";
    public string IconUrl { get; set; } = "";
    public Dictionary<string, int> AscensionMaterials { get; set; } = new();
}
