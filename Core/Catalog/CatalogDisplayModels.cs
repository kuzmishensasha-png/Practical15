namespace Practical1.Core.Catalog;

public sealed record CatalogCard(
    string Id,
    string Name,
    string Element,
    string Weapon,
    int Rarity,
    string ImageUrl,
    bool IsCustom);

public sealed record CatalogDetails(
    string Id,
    string Name,
    string Description,
    string Element,
    string Weapon,
    int Rarity,
    string ImageUrl,
    bool IsCustom);
