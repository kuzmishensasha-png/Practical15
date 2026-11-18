using System.Text.Json;

namespace Practical1.Infra.Remote;

public sealed class GenshinApiClient : IGenshinApiClient, IAmbrCatalogApi
{
    private readonly HttpClient httpClient;
    private string? version;
    private bool versionChecked;
    private IReadOnlyList<AmbrSummary>? characters;
    private IReadOnlyList<AmbrSummary>? weapons;

    public GenshinApiClient(HttpClient httpClient) => this.httpClient = httpClient;

    public async Task<IReadOnlyList<AmbrSummary>> GetCharacterSummariesAsync()
    {
        string json = await RequestAsync("avatar");
        characters = AmbrJsonMapper.CharacterList(json);
        return characters;
    }

    public async Task<IReadOnlyList<string>> GetCharacterIdsAsync()
        => (await GetCharacterSummariesAsync()).Select(x => x.Id).ToList();

    public async Task<RemoteCharacterData?> GetCharacterAsync(string id)
    {
        string resolved = await ResolveAsync(id, character: true);
        return AmbrJsonMapper.CharacterDetail(
            await RequestAsync("avatar/" + Uri.EscapeDataString(resolved)));
    }

    public async Task<IReadOnlyList<AmbrSummary>> GetWeaponSummariesAsync()
    {
        string json = await RequestAsync("weapon");
        weapons = AmbrJsonMapper.WeaponList(json);
        return weapons;
    }

    public async Task<IReadOnlyList<string>> GetWeaponIdsAsync()
        => (await GetWeaponSummariesAsync()).Select(x => x.Id).ToList();

    public async Task<RemoteWeaponData?> GetWeaponAsync(string id)
    {
        string resolved = await ResolveAsync(id, character: false);
        return AmbrJsonMapper.WeaponDetail(
            await RequestAsync("weapon/" + Uri.EscapeDataString(resolved)));
    }

    private async Task<string> ResolveAsync(string query, bool character)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("An ID or name is required.", nameof(query));
        query = query.Trim();
        if (long.TryParse(query, out _)) return query;

        IReadOnlyList<AmbrSummary> entries = character
            ? characters ?? await GetCharacterSummariesAsync()
            : weapons ?? await GetWeaponSummariesAsync();
        AmbrSummary? match = entries.FirstOrDefault(x =>
            x.Name.Equals(query, StringComparison.OrdinalIgnoreCase));
        if (match == null)
            throw new ArgumentException($"No { (character ? "character" : "weapon") } named '{query}'. Try a numeric Ambr ID.");
        return match.Id;
    }

    private async Task<string> RequestAsync(string endpoint)
    {
        if (!versionChecked)
        {
            versionChecked = true;
            try
            {
                using JsonDocument document = JsonDocument.Parse(
                    await httpClient.GetStringAsync("static/version"));
                if (document.RootElement.TryGetProperty("data", out var data)
                    && data.TryGetProperty("vh", out var vh))
                    version = vh.GetString();
            }
            catch (Exception error) when (error is HttpRequestException
                or TaskCanceledException or JsonException or InvalidOperationException)
            {
                version = null;
            }
        }
        string path = "en/" + endpoint;
        if (!string.IsNullOrEmpty(version))
            path += "?vh=" + Uri.EscapeDataString(version);
        return await httpClient.GetStringAsync(path);
    }
}
