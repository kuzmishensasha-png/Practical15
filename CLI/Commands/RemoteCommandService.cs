using Practical1.Infra.Remote;

namespace Practical1.CLI;

public class RemoteCommandService
{
    private readonly IGenshinApiClient api;
    private readonly IPresenter presenter;

    public RemoteCommandService(
        IGenshinApiClient api,
        IPresenter presenter)
    {
        this.api = api;
        this.presenter = presenter;
    }

    public async Task ListCharactersAsync()
    {
        try
        {
            IReadOnlyList<string> characters = await api.GetCharacterIdsAsync();
            foreach (string character in characters) presenter.ShowLine(character);
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException
            or System.Text.Json.JsonException)
        {
            presenter.ShowLine($"Cannot get Ambr characters: {error.Message}");
        }
    }

    public async Task ListWeaponsAsync()
    {
        try
        {
            IReadOnlyList<string> weapons = await api.GetWeaponIdsAsync();
            foreach (string weapon in weapons) presenter.ShowLine(weapon);
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException
            or System.Text.Json.JsonException)
        {
            presenter.ShowLine($"Cannot get Ambr weapons: {error.Message}");
        }
    }
}
