using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Practical1.Infra.Remote;

public static class AmbrJsonMapper
{
    private const string AssetBase = "https://gi.yatta.moe/assets/UI/";

    public static IReadOnlyList<AmbrSummary> CharacterList(string json)
    {
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement items = ItemContainer(doc.RootElement);
        return Entries(items)
            .Select(entry => CharacterSummary(entry.Value, entry.Key))
            .Where(item => item.Id.Length > 0 && item.Name.Length > 0)
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static IReadOnlyList<AmbrSummary> WeaponList(string json)
    {
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement items = ItemContainer(doc.RootElement);
        return Entries(items)
            .Select(entry => new AmbrSummary
            {
                Id = Text(Prop(entry.Value, "id"), entry.Key),
                Name = Text(Prop(entry.Value, "name")),
                Weapon = WeaponType(Text(Prop(entry.Value, "type"))),
                Rarity = Integer(Prop(entry.Value, "rank")),
                IconUrl = AssetUrl(Text(Prop(entry.Value, "icon")))
            })
            .Where(item => item.Id.Length > 0 && item.Name.Length > 0)
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static RemoteCharacterData CharacterDetail(string json)
    {
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement data = Data(doc.RootElement);
        AmbrSummary s = CharacterSummary(data, "");
        if (string.IsNullOrWhiteSpace(s.Id) || string.IsNullOrWhiteSpace(s.Name))
            throw new JsonException("Ambr character detail has no ID or name.");
        JsonElement fetter = Prop(data, "fetter");
        RemoteCharacterData result = new()
        {
            Id = s.Id,
            Name = s.Name,
            Rarity = s.Rarity,
            Vision = s.Vision,
            Weapon = s.Weapon,
            IconUrl = s.IconUrl,
            ArtworkUrl = s.ArtworkUrl,
            SpecialStat = s.SpecialStat,
            ReleaseDate = s.ReleaseDate,
            IsBeta = s.IsBeta,
            Region = s.Region,
            Description = Clean(Text(Prop(fetter, "detail"))),
            Title = Text(Prop(fetter, "title")),
            Affiliation = Text(Prop(fetter, "native")),
            ConstellationName = Text(Prop(fetter, "constellation")),
            VoiceActors = StringDictionary(Prop(fetter, "cv")),
            AscensionMaterials = IntegerDictionary(Prop(data, "ascension"))
        };
        JsonElement birthday = Prop(data, "birthday");
        if (birthday.ValueKind == JsonValueKind.Array)
        {
            int[] day = birthday.EnumerateArray().Take(2).Select(Integer).ToArray();
            if (day.Length == 2 && day[0] > 0 && day[1] > 0)
                result.Birthday = $"{day[1]:00}.{day[0]:00}";
        }
        JsonElement upgrade = Prop(data, "upgrade");
        foreach (JsonElement stat in Values(Prop(upgrade, "prop")))
        {
            result.BaseStats.Add(new AmbrStat
            {
                Name = StatName(Text(Prop(stat, "propType"))),
                BaseValue = Number(Prop(stat, "initValue")),
                GrowthCurve = Text(Prop(stat, "type"))
            });
        }
        foreach (JsonElement phase in Values(Prop(upgrade, "promote")))
        {
            result.Ascensions.Add(new AmbrAscension
            {
                Phase = Integer(Prop(phase, "promoteLevel")),
                MaxLevel = Integer(Prop(phase, "unlockMaxLevel")),
                Mora = Integer(Prop(phase, "coinCost")),
                AdventureRank = Integer(Prop(phase, "requiredPlayerLevel")),
                Materials = IntegerDictionary(Prop(phase, "costItems")),
                BonusStats = NumberDictionary(Prop(phase, "addProps"))
            });
        }
        foreach (JsonElement talent in Values(Prop(data, "talent")))
        {
            AmbrTalent value = new()
            {
                Type = TalentType(Text(Prop(talent, "type"))),
                Name = Text(Prop(talent, "name")),
                Description = Clean(Text(Prop(talent, "description"))),
                IconUrl = AssetUrl(Text(Prop(talent, "icon"))),
                Cooldown = NullableNumber(Prop(talent, "cooldown")),
                EnergyCost = NullableInteger(Prop(talent, "cost"))
            };
            if (value.Name.Length > 0)
            {
                result.Talents.Add(value);
                if (value.Type != "Passive")
                    result.SkillTalents.Add(new RemoteTalentData
                    {
                        Name = value.Name, Description = value.Description
                    });
            }
        }
        foreach (JsonElement star in Values(Prop(data, "constellation")))
        {
            result.Constellations.Add(new AmbrConstellation
            {
                Name = Text(Prop(star, "name")),
                Description = Clean(Text(Prop(star, "description"))),
                IconUrl = AssetUrl(Text(Prop(star, "icon")))
            });
        }
        return result;
    }

    public static RemoteWeaponData WeaponDetail(string json)
    {
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement data = Data(doc.RootElement);
        if (string.IsNullOrWhiteSpace(Text(Prop(data, "id")))
            || string.IsNullOrWhiteSpace(Text(Prop(data, "name"))))
            throw new JsonException("Ambr weapon detail has no ID or name.");
        JsonElement affix = Values(Prop(data, "affix"))
            .FirstOrDefault(x => x.ValueKind == JsonValueKind.Object);
        string passive = "";
        foreach (JsonElement upgrade in Values(Prop(affix, "upgrade")))
        {
            passive = Clean(upgrade.ValueKind == JsonValueKind.String
                ? Text(upgrade) : Text(Prop(upgrade, "description")));
            if (passive.Length > 0) break;
        }
        var stats = Values(Prop(Prop(data, "upgrade"), "prop")).ToList();
        var attack = stats.FirstOrDefault(e =>
            string.Equals(Text(Prop(e, "propType")), "FIGHT_PROP_BASE_ATTACK",
                StringComparison.OrdinalIgnoreCase));
        return new RemoteWeaponData
        {
            Id = Text(Prop(data, "id")),
            Name = Text(Prop(data, "name")),
            Type = WeaponType(Text(Prop(data, "type"))),
            Rarity = Integer(Prop(data, "rank")),
            Description = Clean(Text(Prop(data, "description"))),
            IconUrl = AssetUrl(Text(Prop(data, "icon"))),
            BaseAttack = (int)Math.Round(Number(Prop(attack, "initValue")), MidpointRounding.AwayFromZero),
            SubStat = string.Join(", ", stats.Where(x => x.ValueKind == JsonValueKind.Object
                && !string.Equals(Text(Prop(x, "propType")), "FIGHT_PROP_BASE_ATTACK", StringComparison.OrdinalIgnoreCase))
                .Select(x => $"{StatName(Text(Prop(x, "propType")))} {Number(Prop(x, "initValue")):0.##}")),
            PassiveName = Text(Prop(affix, "name")),
            PassiveDesc = passive,
            AscensionMaterials = IntegerDictionary(Prop(data, "ascension"))
        };
    }

    public static string AssetUrl(string icon)
    {
        if (string.IsNullOrWhiteSpace(icon)) return "";
        if (Uri.TryCreate(icon, UriKind.Absolute, out Uri? uri))
            return uri.Scheme == Uri.UriSchemeHttps ? uri.ToString() : "";
        string file = icon.Trim().TrimStart('/');
        if (!file.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) file += ".png";
        return AssetBase + string.Join("/", file.Split('/').Select(Uri.EscapeDataString));
    }

    public static string Element(string element) => element switch
    {
        "Fire" => "Pyro", "Water" => "Hydro", "Wind" => "Anemo",
        "Electric" => "Electro", "Ice" => "Cryo", "Rock" => "Geo",
        "Grass" => "Dendro", "None" => "None", _ => element
    };

    public static string WeaponType(string type) => type switch
    {
        "WEAPON_BOW" => "Bow", "WEAPON_CATALYST" => "Catalyst",
        "WEAPON_CLAYMORE" => "Claymore", "WEAPON_SWORD_ONE_HAND" => "Sword",
        "WEAPON_POLE" => "Polearm", _ => type
    };

    public static string StatName(string prop) => prop switch
    {
        "FIGHT_PROP_BASE_HP" => "HP", "FIGHT_PROP_BASE_ATTACK" => "ATK",
        "FIGHT_PROP_BASE_DEFENSE" => "DEF", "FIGHT_PROP_HP_PERCENT" => "HP %",
        "FIGHT_PROP_ATTACK_PERCENT" => "ATK %", "FIGHT_PROP_DEFENSE_PERCENT" => "DEF %",
        "FIGHT_PROP_CRITICAL" => "CRIT Rate", "FIGHT_PROP_CRITICAL_HURT" => "CRIT DMG",
        "FIGHT_PROP_ELEMENT_MASTERY" => "Elemental Mastery",
        "FIGHT_PROP_CHARGE_EFFICIENCY" => "Energy Recharge",
        "FIGHT_PROP_HEAL_ADD" => "Healing Bonus",
        "FIGHT_PROP_WATER_ADD_HURT" => "Hydro DMG Bonus",
        "FIGHT_PROP_FIRE_ADD_HURT" => "Pyro DMG Bonus",
        "FIGHT_PROP_ELEC_ADD_HURT" => "Electro DMG Bonus",
        "FIGHT_PROP_WIND_ADD_HURT" => "Anemo DMG Bonus",
        "FIGHT_PROP_ICE_ADD_HURT" => "Cryo DMG Bonus",
        "FIGHT_PROP_ROCK_ADD_HURT" => "Geo DMG Bonus",
        "FIGHT_PROP_GRASS_ADD_HURT" => "Dendro DMG Bonus",
        "FIGHT_PROP_PHYSICAL_ADD_HURT" => "Physical DMG Bonus", _ => prop
    };

    private static AmbrSummary CharacterSummary(JsonElement item, string fallbackId)
    {
        string icon = AssetUrl(Text(Prop(item, "icon")));
        return new AmbrSummary
        {
            Id = Text(Prop(item, "id"), fallbackId),
            Name = Text(Prop(item, "name")),
            Vision = Element(Text(Prop(item, "element"))),
            Weapon = WeaponType(Text(Prop(item, "weaponType"))),
            Rarity = Integer(Prop(item, "rank")),
            IconUrl = icon,
            ArtworkUrl = icon.Contains("AvatarIcon", StringComparison.Ordinal)
                ? icon.Replace("AvatarIcon", "Gacha_AvatarImg", StringComparison.Ordinal) : "",
            SpecialStat = StatName(Text(Prop(item, "specialProp"))),
            Region = Text(Prop(item, "region")),
            ReleaseDate = ReleaseDate(Prop(item, "release")),
            IsBeta = Prop(item, "beta").ValueKind == JsonValueKind.True
        };
    }

    private static JsonElement ItemContainer(JsonElement root)
    {
        JsonElement data = Data(root);
        JsonElement items = Prop(data, "items");
        if (items.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array))
            throw new JsonException("Project Ambr did not return data.items.");
        return items;
    }

    private static JsonElement Data(JsonElement root)
    {
        JsonElement data = Prop(root, "data");
        if (data.ValueKind != JsonValueKind.Object)
            throw new JsonException("Project Ambr did not return a data object.");
        return data;
    }

    private static JsonElement Prop(JsonElement element, string key)
    {
        return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(key, out var result)
            ? result : default;
    }

    private static IEnumerable<KeyValuePair<string, JsonElement>> Entries(JsonElement data)
    {
        if (data.ValueKind == JsonValueKind.Object)
            return data.EnumerateObject().Select(x => new KeyValuePair<string, JsonElement>(x.Name, x.Value)).ToList();
        if (data.ValueKind == JsonValueKind.Array)
            return data.EnumerateArray().Select((x, i) => new KeyValuePair<string, JsonElement>(i.ToString(), x)).ToList();
        return Array.Empty<KeyValuePair<string, JsonElement>>();
    }

    private static IEnumerable<JsonElement> Values(JsonElement data)
    {
        if (data.ValueKind == JsonValueKind.Object)
            return data.EnumerateObject().Select(x => x.Value).ToList();
        if (data.ValueKind == JsonValueKind.Array)
            return data.EnumerateArray().ToList();
        return Array.Empty<JsonElement>();
    }

    private static string Text(JsonElement value, string fallback = "") => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString()?.Trim() ?? fallback,
        JsonValueKind.Number => value.ToString(),
        _ => fallback
    };

    private static int Integer(JsonElement value) => int.TryParse(Text(value), NumberStyles.Any,
        CultureInfo.InvariantCulture, out int result) ? result : 0;

    private static int? NullableInteger(JsonElement value) => value.ValueKind is JsonValueKind.Number or JsonValueKind.String
        ? Integer(value) : null;

    private static double Number(JsonElement value) => double.TryParse(Text(value), NumberStyles.Float,
        CultureInfo.InvariantCulture, out double result) ? result : 0;

    private static double? NullableNumber(JsonElement value) => value.ValueKind is JsonValueKind.Number or JsonValueKind.String
        ? Number(value) : null;

    private static Dictionary<string, int> IntegerDictionary(JsonElement root) =>
        Entries(root).ToDictionary(e => e.Key, e => Integer(e.Value));

    private static Dictionary<string, double> NumberDictionary(JsonElement root) =>
        Entries(root).ToDictionary(e => e.Key, e => Number(e.Value));

    private static Dictionary<string, string> StringDictionary(JsonElement root) =>
        Entries(root).ToDictionary(e => e.Key, e => Text(e.Value));

    private static string ReleaseDate(JsonElement value)
    {
        if (long.TryParse(Text(value), out long stamp))
        {
            try { return DateTimeOffset.FromUnixTimeSeconds(stamp).ToString("yyyy-MM-dd"); }
            catch (ArgumentOutOfRangeException) { return ""; }
        }
        return "";
    }

    private static string TalentType(string type) => type switch
    {
        "0" => "Normal Attack", "1" => "Elemental Skill", "2" => "Elemental Burst",
        "3" => "Passive", _ => type
    };

    private static string Clean(string value) => WebUtility.HtmlDecode(
        Regex.Replace(value.Replace("<br>", "\n", StringComparison.OrdinalIgnoreCase), "<[^>]+>", ""))
        .Replace("\\n", "\n").Trim();
}
