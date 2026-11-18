using System.Net;
using System.Text;
using Practical1.Infra.Remote;

namespace Practical1.Tests.Infra.Remote;

public class GenshinApiClientTests
{
    private const string ListJson = """
        {"data":{"items":{"10000021":{"id":10000021,"name":"Amber","rank":4,"element":"Fire",
        "weaponType":"WEAPON_BOW","icon":"UI_AvatarIcon_Amber","region":"Mondstadt"},
        "10000002":{"id":10000002,"name":"Kamisato Ayaka","rank":5,"element":"Ice",
        "weaponType":"WEAPON_SWORD_ONE_HAND","icon":"UI_AvatarIcon_Ayaka"}}}}
        """;

    private const string DetailJson = """
        {"data":{"id":"10000021","name":"Amber","rank":4,"element":"Fire","weaponType":"WEAPON_BOW",
        "icon":"UI_AvatarIcon_Amber","region":"Mondstadt","specialProp":"FIGHT_PROP_ATTACK_PERCENT",
        "birthday":[8,10],"fetter":{"title":"Outrider","detail":"A cheerful knight.",
        "native":"Knights of Favonius","constellation":"Lepus","cv":{"en":"Example voice actor"}},
        "upgrade":{"prop":[
        {"propType":"FIGHT_PROP_BASE_HP","initValue":793,"type":"GROW_CURVE_HP_S4"},
        {"propType":"FIGHT_PROP_BASE_ATTACK","initValue":19.5,"type":"GROW_CURVE_ATTACK_S4"},
        {"propType":"FIGHT_PROP_BASE_DEFENSE","initValue":50,"type":"GROW_CURVE_DEFENSE_S4"}],
        "promote":{"1":{"promoteLevel":1,"unlockMaxLevel":40,"coinCost":20000,
        "requiredPlayerLevel":15,"costItems":{"1001":3},"addProps":{"FIGHT_PROP_ATTACK_PERCENT":0.06}}}},
        "ascension":{"12345":4},
        "talent":{"a":{"type":1,"name":"Explosive Puppet","description":"Places Baron Bunny",
        "icon":"Skill_S_Amber_01","cooldown":15,"cost":null}},
        "constellation":{"1":{"name":"One Arrow","description":"Extra shot","icon":"Talent_Amber_01"}}}}
        """;

    private const string WeaponListJson = """
        {"data":{"items":{"11301":{"id":11301,"rank":3,"type":"WEAPON_SWORD_ONE_HAND",
        "name":"Cool Steel","icon":"UI_EquipIcon_Sword_Steel"}}}}
        """;

    private const string WeaponDetailJson = """
        {"data":{"id":11301,"rank":3,"type":"WEAPON_SWORD_ONE_HAND","name":"Cool Steel",
        "description":"A sword.","icon":"UI_EquipIcon_Sword_Steel", "ascension":{},
        "upgrade":{"prop":[{"propType":"FIGHT_PROP_BASE_ATTACK","initValue":39,"type":"GROW_CURVE_ATTACK_101"}],"promote":[]},
        "affix":{"1101":{"name":"Bane of Water and Ice","upgrade":{"1":"Increases damage."}}}}}
        """;

    private sealed class Handler : HttpMessageHandler
    {
        public List<string> Paths { get; } = new();
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            string path = request.RequestUri!.AbsolutePath;
            Paths.Add(path);
            string json = path switch
            {
                "/api/v2/static/version" => "{\"data\":{\"vh\":\"abc123\"}}",
                "/api/v2/en/avatar" => ListJson,
                "/api/v2/en/avatar/10000021" => DetailJson,
                "/api/v2/en/weapon" => WeaponListJson,
                "/api/v2/en/weapon/11301" => WeaponDetailJson,
                _ => throw new Exception("Unexpected URL: " + path)
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }

    [Fact]
    public async Task List_MapsNumericIds_Elements_Icons()
    {
        Handler handler = new();
        GenshinApiClient api = Client(handler);
        var entries = await api.GetCharacterSummariesAsync();
        Assert.Equal(2, entries.Count);
        var amber = Assert.Single(entries.Where(e => e.Id == "10000021"));
        Assert.Equal("Pyro", amber.Vision);
        Assert.Equal("Bow", amber.Weapon);
        Assert.Contains("/UI_AvatarIcon_Amber.png", amber.IconUrl);
        Assert.Contains("/UI_Gacha_AvatarImg_Amber.png", amber.ArtworkUrl);
        Assert.Equal(2, (await api.GetCharacterIdsAsync()).Count);
        Assert.Contains(handler.Paths, p => p.EndsWith("static/version"));
    }

    [Fact]
    public async Task Character_MapsRealBaseStatsTalentConstellationAndBio()
    {
        GenshinApiClient api = Client(new Handler());
        var x = await api.GetCharacterAsync("10000021");
        Assert.NotNull(x);
        Assert.Equal(793d, Assert.Single(x.BaseStats.Where(s => s.Name == "HP")).BaseValue);
        Assert.Equal(50d, Assert.Single(x.BaseStats.Where(s => s.Name == "DEF")).BaseValue);
        Assert.Equal("Explosive Puppet", Assert.Single(x.Talents).Name);
        Assert.Single(x.Constellations);
        Assert.Single(x.Ascensions);
        Assert.Equal("10.08", x.Birthday);
        Assert.Equal("Knights of Favonius", x.Affiliation);
    }

    [Fact]
    public void Weapon_WithoutPassive_ParsesSafely()
    {
        const string json = """
            {"data":{"id":11101,"name":"Dull Blade","rank":1,"type":"WEAPON_SWORD_ONE_HAND",
            "upgrade":{"prop":[{"propType":"FIGHT_PROP_BASE_ATTACK","initValue":23}],"promote":[]},
            "affix":null}}
            """;
        var weapon = AmbrJsonMapper.WeaponDetail(json);
        Assert.Equal(23, weapon.BaseAttack);
        Assert.Equal("", weapon.PassiveName);
        Assert.Equal("", weapon.PassiveDesc);
    }

    [Fact]
    public async Task Weapon_ListAndDetailFromAmbr()
    {
        GenshinApiClient api = Client(new Handler());
        var list = await api.GetWeaponSummariesAsync();
        Assert.Equal("Cool Steel", Assert.Single(list).Name);
        var weapon = await api.GetWeaponAsync("Cool Steel");
        Assert.NotNull(weapon);
        Assert.Equal(39, weapon.BaseAttack);
        Assert.Equal("Sword", weapon.Type);
        Assert.Equal("Bane of Water and Ice", weapon.PassiveName);
        Assert.Equal("Increases damage.", weapon.PassiveDesc);
    }

    [Fact]
    public void Mapper_RejectsMissingIdentity_InsteadOfCachingFakeCharacter()
    {
        Assert.Throws<System.Text.Json.JsonException>(() =>
            AmbrJsonMapper.CharacterDetail("""{"data":{"rank":4}}"""));
    }

    [Fact]
    public void Mapper_DoesNotConvertPassiveTalentIntoActiveGameAbility()
    {
        const string json = """
            {"data":{"id":"10000021","name":"Amber","rank":4,
            "talent":{"active":{"name":"Skill","type":1},
            "passive":{"name":"Passive","type":3}}}}
            """;
        var character = AmbrJsonMapper.CharacterDetail(json);
        Assert.Equal(2, character.Talents.Count);
        Assert.Single(character.SkillTalents);
        Assert.Equal("Skill", character.SkillTalents[0].Name);
    }

    [Fact]
    public async Task VersionBlocked_StillRequestsCharacterListWithoutVersionHash()
    {
        using HttpClient client = new(new VersionBlockedHandler())
        {
            BaseAddress = new Uri("https://gi.yatta.moe/api/v2/")
        };
        GenshinApiClient api = new(client);
        var list = await api.GetCharacterSummariesAsync();
        Assert.Equal(2, list.Count);
    }

    private sealed class VersionBlockedHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/static/version"))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden));
            Assert.Equal("/api/v2/en/avatar", request.RequestUri.AbsolutePath);
            Assert.Equal("", request.RequestUri.Query);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ListJson, Encoding.UTF8, "application/json")
            });
        }
    }

    private static GenshinApiClient Client(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://gi.yatta.moe/api/v2/") });
}
