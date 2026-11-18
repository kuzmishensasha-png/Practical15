using System.Text.Json;

namespace Practical1.Core.Catalog;

public sealed record CatalogApiProfile(
    string Name,
    string Description,
    string Vision,
    string Weapon,
    int Rarity);

public sealed class CatalogJsonParser
{
    public IReadOnlyList<string> ParseIds(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("Expected a JSON array with character IDs.");
        }

        List<string> ids = new();
        foreach (JsonElement entry in document.RootElement.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.String)
            {
                throw new JsonException("Character ID must be a string.");
            }

            string? id = entry.GetString();
            if (!string.IsNullOrWhiteSpace(id))
            {
                ids.Add(id.Trim());
            }
        }

        return ids.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public CatalogApiProfile ParseProfile(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("Expected a JSON object with character details.");
        }

        int rarity = 0;
        if (root.TryGetProperty("rarity", out JsonElement rarityValue))
        {
            if (rarityValue.ValueKind == JsonValueKind.Number)
            {
                if (!rarityValue.TryGetInt32(out rarity))
                {
                    throw new JsonException("Invalid rarity number.");
                }
            }
            else if (rarityValue.ValueKind == JsonValueKind.String)
            {
                if (!int.TryParse(rarityValue.GetString(), out rarity))
                {
                    throw new JsonException("Invalid rarity value.");
                }
            }
        }

        return new CatalogApiProfile(
            ReadString(root, "name"),
            ReadString(root, "description"),
            ReadString(root, "vision"),
            ReadString(root, "weapon"),
            rarity);
    }

    private static string ReadString(JsonElement value, string property)
    {
        if (!value.TryGetProperty(property, out JsonElement field)
            || field.ValueKind == JsonValueKind.Null)
        {
            return "";
        }

        if (field.ValueKind != JsonValueKind.String)
        {
            throw new JsonException($"Invalid {property} field.");
        }

        return field.GetString()?.Trim() ?? "";
    }
}
