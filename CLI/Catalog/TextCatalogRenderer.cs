using System.Globalization;
using System.Text;

namespace Practical1.CLI.Catalog;

public class TextCatalogRenderer : ICatalogRenderer
{
    public string RenderList(IReadOnlyList<CatalogCharacter> characters)
    {
        if (characters.Count == 0) return "Catalog empty. Use update, load or create.";
        StringBuilder sb = new();
        foreach (CatalogCharacter x in characters.OrderBy(x => x.Name))
            sb.AppendLine($"[{(x.IsCustom ? "user" : "Ambr")}] {x.Name} (id: {x.Id}) " +
                $"{x.Vision} | {x.Weapon} | {x.Rarity} stars");
        sb.AppendLine($"Total: {characters.Count}");
        return sb.ToString().TrimEnd();
    }

    public string RenderDetails(CatalogCharacter x)
    {
        StringBuilder sb = new();
        sb.AppendLine($"ID: {x.Id}");
        sb.AppendLine($"Name: {x.Name}");
        sb.AppendLine($"Source: {(x.IsCustom ? "user" : "Project Ambr")}");
        sb.AppendLine($"Title: {Show(x.Title)}");
        sb.AppendLine($"Rarity: {(x.Rarity > 0 ? x.Rarity.ToString() : "unknown")}");
        sb.AppendLine($"Element: {Show(x.Vision)}");
        sb.AppendLine($"Weapon: {Show(x.Weapon)}");
        sb.AppendLine($"Region: {Show(x.Region)}");
        sb.AppendLine($"Release: {Show(x.ReleaseDate)}");
        if (x.IsBeta) sb.AppendLine("Status: beta/unreleased data");
        sb.AppendLine($"Affiliation: {Show(x.Affiliation)}");
        sb.AppendLine($"Birthday: {Show(x.Birthday)}");
        sb.AppendLine($"Ascension bonus: {Show(x.SpecialStat)}");
        sb.AppendLine($"Constellation: {Show(x.ConstellationName)}");
        sb.AppendLine($"Description: {Show(x.Description)}");
        sb.AppendLine($"Icon: {Show(x.ImageUrl)}");
        sb.AppendLine($"Artwork: {Show(x.ArtworkUrl)}");
        if (x.BaseStats.Count > 0)
            sb.AppendLine("Base stats (level 1): " + string.Join(", ", x.BaseStats.Select(s =>
                $"{s.Name} {s.BaseValue.ToString("0.##", CultureInfo.InvariantCulture)}")));
        return sb.ToString().TrimEnd();
    }

    public string RenderStats(CatalogCharacter x)
    {
        StringBuilder sb = new($"{x.Name} — base statistics (level 1, without equipment):\n");
        if (x.BaseStats.Count == 0) sb.AppendLine("No stats in local data.");
        foreach (var stat in x.BaseStats)
            sb.AppendLine($"  {stat.Name}: {stat.BaseValue.ToString("0.##", CultureInfo.InvariantCulture)} (curve: {stat.GrowthCurve})");
        sb.AppendLine("Ascension phases:");
        foreach (var phase in x.Ascensions.OrderBy(a => a.Phase))
        {
            sb.AppendLine($"  Phase {phase.Phase}: max level {phase.MaxLevel}, Mora {phase.Mora}, " +
                $"adventure rank {phase.AdventureRank}, bonus: {string.Join(", ", phase.BonusStats.Select(p => AmbrStatName(p.Key) + "=" + p.Value.ToString("0.##", CultureInfo.InvariantCulture)))}");
            if (phase.Materials.Count > 0)
                sb.AppendLine("    Material IDs: " + string.Join(", ", phase.Materials.Select(p => $"{p.Key} x{p.Value}")));
        }
        if (x.AscensionMaterials.Count > 0)
            sb.AppendLine("Ascension material IDs (rarity): " + string.Join(", ",
                x.AscensionMaterials.Select(p => $"{p.Key} (rarity {p.Value})")));
        sb.AppendLine($"Special ascension stat: {Show(x.SpecialStat)}");
        return sb.ToString().TrimEnd();
    }

    public string RenderTalents(CatalogCharacter x)
    {
        if (x.Talents.Count == 0) return "No talents in local data.";
        StringBuilder sb = new($"Talents — {x.Name}:\n");
        foreach (var t in x.Talents)
        {
            sb.AppendLine($"  [{t.Type}] {t.Name}");
            sb.AppendLine("    " + t.Description);
            if (t.Cooldown.HasValue) sb.AppendLine($"    Cooldown: {t.Cooldown.Value:0.##} s");
            if (t.EnergyCost.HasValue) sb.AppendLine($"    Energy: {t.EnergyCost.Value}");
        }
        return sb.ToString().TrimEnd();
    }

    public string RenderConstellations(CatalogCharacter x)
    {
        if (x.Constellations.Count == 0) return "No constellations in local data.";
        StringBuilder sb = new($"Constellations — {x.Name}:\n");
        for (int i = 0; i < x.Constellations.Count; i++)
            sb.AppendLine($"  C{i + 1}: {x.Constellations[i].Name}\n    {x.Constellations[i].Description}");
        return sb.ToString().TrimEnd();
    }

    public string RenderBiography(CatalogCharacter x)
    {
        StringBuilder sb = new($"Biography — {x.Name}:\n");
        sb.AppendLine($"Title: {Show(x.Title)}");
        sb.AppendLine($"Region: {Show(x.Region)}; Affiliation: {Show(x.Affiliation)}");
        sb.AppendLine($"Birthday: {Show(x.Birthday)}");
        sb.AppendLine($"Story: {Show(x.Description)}");
        if (x.VoiceActors.Count > 0)
            foreach (var actor in x.VoiceActors)
                sb.AppendLine($"Voice actor ({actor.Key}): {actor.Value}");
        return sb.ToString().TrimEnd();
    }

    private static string Show(string? value) => string.IsNullOrWhiteSpace(value) ? "not specified" : value;
    private static string AmbrStatName(string value) => Practical1.Infra.Remote.AmbrJsonMapper.StatName(value);
}
