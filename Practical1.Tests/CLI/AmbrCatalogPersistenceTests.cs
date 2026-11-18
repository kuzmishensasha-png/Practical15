using Practical1.CLI.Catalog;
using Practical1.Infra.Data;
using Practical1.Infra.Remote;

namespace Practical1.Tests.CLI;

public class AmbrCatalogPersistenceTests
{
    private sealed class FakeAmbr : IGenshinApiClient, IAmbrCatalogApi
    {
        public Task<IReadOnlyList<AmbrSummary>> GetCharacterSummariesAsync()
            => Task.FromResult<IReadOnlyList<AmbrSummary>>(new[]
            {
                new AmbrSummary { Id = "10000021", Name = "Amber", Vision = "Pyro",
                    Weapon = "Bow", Rarity = 4,
                    IconUrl = "https://gi.yatta.moe/assets/UI/UI_AvatarIcon_Amber.png" }
            });
        public async Task<IReadOnlyList<string>> GetCharacterIdsAsync()
            => (await GetCharacterSummariesAsync()).Select(x => x.Id).ToList();
        public Task<RemoteCharacterData?> GetCharacterAsync(string id)
            => Task.FromResult<RemoteCharacterData?>(new RemoteCharacterData
            {
                Id = id, Name = "Amber", Description = "Outrider", Vision = "Pyro", Weapon = "Bow",
                Rarity = 4, BaseStats = new List<AmbrStat> { new() { Name = "HP", BaseValue = 793 } },
                Talents = new List<AmbrTalent> { new() { Name = "Explosive Puppet" } }
            });
        public Task<IReadOnlyList<AmbrSummary>> GetWeaponSummariesAsync()
            => Task.FromResult<IReadOnlyList<AmbrSummary>>(Array.Empty<AmbrSummary>());
        public Task<IReadOnlyList<string>> GetWeaponIdsAsync()
            => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        public Task<RemoteWeaponData?> GetWeaponAsync(string id)
            => Task.FromResult<RemoteWeaponData?>(null);
    }

    [Fact]
    public async Task FullProfile_PersistsBetweenRuns_WithoutSecondNetworkRequest()
    {
        string path = Path.Combine(Path.GetTempPath(), "ambr-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            CharacterCatalog catalog = new(new FakeAmbr(), new JsonDataStorage());
            await catalog.UpdateAsync();
            var amber = await catalog.FindAsync("Amber");
            Assert.NotNull(amber);
            Assert.True(amber.DetailsLoaded);
            catalog.Save(path);

            CharacterCatalog loaded = new(new OfflineApi(), new JsonDataStorage());
            Assert.True(loaded.Load(path));
            var cached = await loaded.FindAsync("10000021");
            Assert.NotNull(cached);
            Assert.Equal(793d, Assert.Single(cached.BaseStats).BaseValue);
            Assert.Equal("Explosive Puppet", Assert.Single(cached.Talents).Name);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    private sealed class OfflineApi : IGenshinApiClient
    {
        public Task<IReadOnlyList<string>> GetCharacterIdsAsync() => throw new Exception("No network allowed");
        public Task<RemoteCharacterData?> GetCharacterAsync(string id) => throw new Exception("No network allowed");
        public Task<IReadOnlyList<string>> GetWeaponIdsAsync() => throw new Exception("No network allowed");
        public Task<RemoteWeaponData?> GetWeaponAsync(string id) => throw new Exception("No network allowed");
    }
}
