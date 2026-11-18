using Practical1.Core.Catalog;
using Practical1.Infra.Data;
using Practical1.Infra.Remote;

namespace Practical1.CLI.Catalog;

public class CharacterCatalog
{
    private readonly IGenshinApiClient api;
    private readonly IDataStorage storage;
    private readonly List<CatalogCharacter> characters = new();
    public IReadOnlyList<CatalogCharacter> Characters => characters.AsReadOnly();

    public CharacterCatalog(IGenshinApiClient api, IDataStorage storage)
    {
        this.api = api;
        this.storage = storage;
    }

    public async Task<int> UpdateAsync()
    {
        IReadOnlyList<AmbrSummary> remote = api is IAmbrCatalogApi ambr
            ? await ambr.GetCharacterSummariesAsync()
            : (await api.GetCharacterIdsAsync()).Select(id => new AmbrSummary { Id = id, Name = id }).ToList();
        HashSet<string> ids = remote.Where(x => !string.IsNullOrWhiteSpace(x.Id))
            .Select(x => x.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (ids.Count == 0)
            throw new HttpRequestException("Project Ambr returned an empty character list.");

        characters.RemoveAll(x => !x.IsCustom && !ids.Contains(x.Id));
        foreach (AmbrSummary source in remote.Where(x => ids.Contains(x.Id)))
        {
            CatalogCharacter? item = characters.FirstOrDefault(x =>
                !x.IsCustom && x.Id.Equals(source.Id, StringComparison.OrdinalIgnoreCase));
            if (item == null)
            {
                item = new CatalogCharacter { Id = source.Id };
                characters.Add(item);
            }
            item.DetailsLoaded = false;
            item.Name = source.Name;
            item.Vision = source.Vision;
            item.Weapon = source.Weapon;
            item.Rarity = source.Rarity;
            item.Region = source.Region;
            item.SpecialStat = source.SpecialStat;
            item.ReleaseDate = source.ReleaseDate;
            item.IsBeta = source.IsBeta;
            item.ImageUrl = source.IconUrl;
            item.ArtworkUrl = source.ArtworkUrl;
        }
        return ids.Count;
    }

    public CatalogCharacter? Lookup(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return null;
        string text = query.Trim();
        return characters.FirstOrDefault(x =>
            x.Id.Equals(text, StringComparison.OrdinalIgnoreCase) ||
            x.Name.Equals(text, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<CatalogCharacter?> FindAsync(string query)
    {
        CatalogCharacter? item = Lookup(query);
        if (item == null || item.IsCustom || item.DetailsLoaded) return item;
        RemoteCharacterData? data = await api.GetCharacterAsync(item.Id);
        if (data == null) return item;
        item.Name = string.IsNullOrWhiteSpace(data.Name) ? item.Name : data.Name;
        item.Description = data.Description;
        item.Vision = data.Vision;
        item.Weapon = data.Weapon;
        item.Rarity = data.Rarity;
        item.Title = data.Title;
        item.Region = data.Region;
        item.Affiliation = data.Affiliation;
        item.ConstellationName = data.ConstellationName;
        item.Birthday = data.Birthday;
        item.SpecialStat = data.SpecialStat;
        item.ReleaseDate = data.ReleaseDate;
        item.IsBeta = data.IsBeta;
        item.VoiceActors = data.VoiceActors;
        item.BaseStats = data.BaseStats;
        item.Ascensions = data.Ascensions;
        item.Talents = data.Talents;
        item.Constellations = data.Constellations;
        item.AscensionMaterials = data.AscensionMaterials;
        if (!string.IsNullOrWhiteSpace(data.IconUrl)) item.ImageUrl = data.IconUrl;
        if (!string.IsNullOrWhiteSpace(data.ArtworkUrl)) item.ArtworkUrl = data.ArtworkUrl;
        item.DetailsLoaded = true;
        return item;
    }

    public async Task<IReadOnlyList<AmbrSummary>> GetWeaponSummariesAsync()
    {
        if (api is IAmbrCatalogApi ambr) return await ambr.GetWeaponSummariesAsync();
        return (await api.GetWeaponIdsAsync()).Select(id => new AmbrSummary { Id = id, Name = id }).ToList();
    }

    public Task<RemoteWeaponData?> GetWeaponAsync(string idOrName) => api.GetWeaponAsync(idOrName);

    public IReadOnlyList<CatalogCard> GetCards(string? element = null)
    {
        IEnumerable<CatalogCharacter> items = characters;
        if (!string.IsNullOrWhiteSpace(element))
            items = items.Where(x => x.Vision.Equals(element.Trim(), StringComparison.OrdinalIgnoreCase));
        return items.OrderBy(x => x.Name).Select(x => new CatalogCard(
            x.Id, x.Name, x.Vision, x.Weapon, x.Rarity, x.ImageUrl, x.IsCustom)).ToList();
    }

    public async Task<CatalogDetails?> GetDetailsAsync(string query)
    {
        CatalogCharacter? x = await FindAsync(query);
        return x == null ? null : new CatalogDetails(
            x.Id, x.Name, x.Description, x.Vision, x.Weapon, x.Rarity, x.ImageUrl, x.IsCustom);
    }

    public CatalogCharacter CreateCustom(string name, string description, string vision,
        string weapon, int rarity, string imageUrl)
    {
        name = CatalogBusinessRules.RequiredText(name, "Name");
        description = CatalogBusinessRules.RequiredText(description, "Description");
        vision = CatalogBusinessRules.RequiredText(vision, "Element");
        weapon = CatalogBusinessRules.RequiredText(weapon, "Weapon");
        rarity = CatalogBusinessRules.ValidRarity(rarity);
        imageUrl = CatalogBusinessRules.ValidImageUrl(imageUrl);
        if (characters.Any(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("A character with this name already exists.");
        CatalogCharacter item = new()
        {
            Id = "user-" + Guid.NewGuid().ToString("N")[..8],
            Name = name, Description = description, Vision = vision, Weapon = weapon,
            Rarity = rarity, ImageUrl = imageUrl, IsCustom = true, DetailsLoaded = true
        };
        characters.Add(item);
        return item;
    }

    public void Save(string fileName) => storage.Save(fileName, new CatalogData
    {
        Characters = characters.ToList()
    });

    public bool Load(string fileName)
    {
        CatalogData? data = storage.Load<CatalogData>(fileName);
        if (data == null) return false;
        List<CatalogCharacter> loaded = (data.Characters ?? new())
            .Where(x => x != null && !string.IsNullOrWhiteSpace(x.Id) && !string.IsNullOrWhiteSpace(x.Name))
            .GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase).Select(group => group.First()).ToList();
        foreach (CatalogCharacter item in loaded)
        {
            item.VoiceActors ??= new();
            item.BaseStats ??= new();
            item.Ascensions ??= new();
            item.Talents ??= new();
            item.Constellations ??= new();
            item.AscensionMaterials ??= new();
            item.Name ??= "";
            item.Vision ??= "";
            item.Weapon ??= "";
            item.ImageUrl ??= "";
            item.ArtworkUrl ??= "";
        }
        characters.Clear();
        characters.AddRange(loaded);
        return true;
    }
}
