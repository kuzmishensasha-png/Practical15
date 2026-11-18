using System.Text.Json;
using Practical1.Core.Catalog;
using Practical1.CLI.Catalog;
using Practical1.Infra.Data;
using Practical1.Infra.Remote;

namespace Practical1.Tests.CLI;

public class CatalogBusinessTests
{
    [Fact]
    public void ParseIds_ReadsArrayRemovesDuplicates()
    {
        CatalogJsonParser parser = new();
        IReadOnlyList<string> ids = parser.ParseIds(
            "[\"amber\",\"lisa\",\"Amber\",\"\"]");

        Assert.Equal(2, ids.Count);
        Assert.Equal("amber", ids[0]);
        Assert.Equal("lisa", ids[1]);
    }

    [Fact]
    public void ParseProfile_ReadsJsonFields()
    {
        CatalogJsonParser parser = new();
        CatalogApiProfile profile = parser.ParseProfile(
            "{\"name\":\"Amber\",\"description\":\"Scout\",\"vision\":\"Pyro\",\"weapon\":\"Bow\",\"rarity\":4}");

        Assert.Equal("Amber", profile.Name);
        Assert.Equal("Pyro", profile.Vision);
        Assert.Equal("Bow", profile.Weapon);
        Assert.Equal(4, profile.Rarity);
    }

    [Fact]
    public void ParseIds_RejectsWrongJsonShape()
    {
        Assert.Throws<JsonException>(() =>
            new CatalogJsonParser().ParseIds("{\"characters\":[]}"));
    }

    [Fact]
    public void CreateCustom_RejectsInvalidRarityAndEmptyName()
    {
        CharacterCatalog catalog = new(new FakeApi(), new JsonDataStorage());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            catalog.CreateCustom("Hero", "Story", "Pyro", "Bow", 7, ""));
        Assert.Throws<ArgumentException>(() =>
            catalog.CreateCustom(" ", "Story", "Pyro", "Bow", 4, ""));
    }

    [Fact]
    public async Task GuiModels_AreReadyForListAndDetails()
    {
        CharacterCatalog catalog = new(new FakeApi(), new JsonDataStorage());
        await catalog.UpdateAsync();
        CatalogDetails? amber = await catalog.GetDetailsAsync("amber");
        IReadOnlyList<CatalogCard> pyroCards = catalog.GetCards("Pyro");

        Assert.NotNull(amber);
        Assert.Equal("Amber", amber.Name);
        Assert.Equal("Bow", amber.Weapon);
        Assert.Single(pyroCards);
        Assert.Equal("amber", pyroCards[0].Id);
    }

    private sealed class FakeApi : IGenshinApiClient
    {
        public Task<IReadOnlyList<string>> GetCharacterIdsAsync()
            => Task.FromResult<IReadOnlyList<string>>(new[] { "amber" });

        public Task<RemoteCharacterData?> GetCharacterAsync(string id)
            => Task.FromResult<RemoteCharacterData?>(new RemoteCharacterData
            {
                Name = "Amber", Vision = "Pyro", Weapon = "Bow",
                Description = "Scout", Rarity = 4
            });

        public Task<IReadOnlyList<string>> GetWeaponIdsAsync()
            => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

        public Task<RemoteWeaponData?> GetWeaponAsync(string id)
            => Task.FromResult<RemoteWeaponData?>(null);
    }
}
