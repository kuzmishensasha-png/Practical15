using Practical1.CLI;
using Practical1.Infra.Images;

namespace Practical1.CLI.Catalog;

public class CatalogCommandStrategy : ICommandStrategy
{
    private readonly CharacterCatalog catalog;
    private readonly ICatalogRenderer renderer;
    private readonly IPresenter presenter;
    private readonly CharacterPortraitCache portraits;
    private string currentFile = "catalog.json";

    public CatalogCommandStrategy(
        CharacterCatalog catalog,
        ICatalogRenderer renderer,
        IPresenter presenter,
        CharacterPortraitCache portraits)
    {
        this.catalog = catalog;
        this.renderer = renderer;
        this.presenter = presenter;
        this.portraits = portraits;

        try
        {
            if (catalog.Load(currentFile))
            {
                presenter.ShowLine($"Catalog loaded from {currentFile}.");
            }
        }
        catch (Exception error) when (error is IOException
            or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            presenter.ShowLine($"Cannot load saved catalog: {error.Message}");
        }
    }

    public void RegisterCommands(CommandInterpreter interpreter)
    {
        interpreter.Register("help", request => Help());
        interpreter.Register("update", request => Update());
        interpreter.Register("ls", request => List());
        interpreter.Register("show", Show);
        interpreter.Register("portrait", Portrait);
        interpreter.Register("stats", request => ShowExtra(request, new TextCatalogRenderer().RenderStats));
        interpreter.Register("talents", request => ShowExtra(request, new TextCatalogRenderer().RenderTalents));
        interpreter.Register("constellations", request => ShowExtra(request, new TextCatalogRenderer().RenderConstellations));
        interpreter.Register("bio", request => ShowExtra(request, new TextCatalogRenderer().RenderBiography));
        interpreter.Register("weapons", Weapons);
        interpreter.Register("weapon", Weapon);
        interpreter.Register("create", request => Create());
        interpreter.Register("save", Save);
        interpreter.Register("load", Load);
    }

    private void Help()
    {
        presenter.ShowLine("Catalog commands:");
        presenter.ShowLine("update               - update catalog from Project Ambr");
        presenter.ShowLine("ls                   - list all characters");
        presenter.ShowLine("show <id/name>       - profile + actual level-1 stats");
        presenter.ShowLine("stats <id/name>      - detailed base stats and ascensions");
        presenter.ShowLine("talents <id/name>    - active/passive talents");
        presenter.ShowLine("constellations <id>  - constellations C1-C6");
        presenter.ShowLine("bio <id/name>        - story, region, voice actors");
        presenter.ShowLine("weapons [search]     - list/search weapons from Ambr");
        presenter.ShowLine("weapon <id/name>     - weapon details from Ambr");
        presenter.ShowLine("portrait <id/name>   - download/reuse local portrait");
        presenter.ShowLine("portrait demo        - test portrait cache without internet");
        presenter.ShowLine("portrait all         - cache all available portraits");
        presenter.ShowLine("create               - create a character in a dialog");
        presenter.ShowLine("save [file]          - save catalog as JSON");
        presenter.ShowLine("load [file]          - load catalog from JSON");
        presenter.ShowLine("exit                 - close application");
        presenter.ShowLine("Put names with spaces in quotes: show \"My Hero\"");
    }

    private void Update()
    {
        try
        {
            int remoteCount = catalog.UpdateAsync().GetAwaiter().GetResult();
            catalog.Save(currentFile);
            presenter.ShowLine($"Updated: {remoteCount} API characters. Local characters kept.");
        }
        catch (Exception error) when (error is HttpRequestException
            or TaskCanceledException or IOException or UnauthorizedAccessException
            or System.Text.Json.JsonException)
        {
            presenter.ShowLine($"Cannot update catalog: {error.Message}");
            presenter.ShowLine("You can still use a previously saved catalog.");
        }
    }

    private void List()
    {
        presenter.ShowLine(renderer.RenderList(catalog.Characters));
    }

    private void Show(CommandRequest request)
    {
        if (request.Arguments.Count == 0)
        {
            presenter.ShowLine("Usage: show <id/name>");
            return;
        }

        string name = string.Join(" ", request.Arguments);

        try
        {
            CatalogCharacter? character =
                catalog.FindAsync(name).GetAwaiter().GetResult();

            if (character == null)
            {
                presenter.ShowLine("Character not found. Try update or ls.");
                return;
            }

            SaveFetchedDetails();
            presenter.ShowLine(renderer.RenderDetails(character));
            ShowLocalPortrait(character);
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException
            or System.Text.Json.JsonException or ArgumentException)
        {
            CatalogCharacter? cached = catalog.Characters.FirstOrDefault(character =>
                string.Equals(character.Id, name, StringComparison.OrdinalIgnoreCase)
                || string.Equals(character.Name, name, StringComparison.OrdinalIgnoreCase));

            presenter.ShowLine($"API unavailable: {error.Message}");
            if (cached != null)
            {
                presenter.ShowLine(renderer.RenderDetails(cached));
                ShowLocalPortrait(cached);
            }
        }
    }

    private void ShowLocalPortrait(CatalogCharacter character)
    {
        string? path = portraits.GetCachedPath(character.Id, character.ArtworkUrl)
            ?? portraits.GetCachedPath(character.Id, character.ImageUrl);
        if (path != null)
        {
            presenter.ShowLine($"Local portrait: {path}");
        }
        else if (!string.IsNullOrWhiteSpace(character.ImageUrl))
        {
            presenter.ShowLine($"Local portrait: not cached (use portrait {character.Id}).");
        }
    }

    private void Portrait(CommandRequest request)
    {
        if (request.Arguments.Count == 0)
        {
            presenter.ShowLine("Usage: portrait <id/name> or portrait all");
            return;
        }

        string query = string.Join(" ", request.Arguments);
        if (string.Equals(query, "demo", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                presenter.ShowLine(PortraitCacheDemo.RunAsync(portraits.CacheDirectory)
                    .GetAwaiter().GetResult());
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                presenter.ShowLine($"Cannot run offline demo: {error.Message}");
            }
            return;
        }
        if (string.Equals(query, "all", StringComparison.OrdinalIgnoreCase))
        {
            int saved = 0;
            int failed = 0;
            foreach (CatalogCharacter character in catalog.Characters
                .Where(item => !string.IsNullOrWhiteSpace(item.ImageUrl)))
            {
                if (DownloadPortrait(character))
                {
                    saved++;
                }
                else
                {
                    failed++;
                }
            }

            presenter.ShowLine($"Portraits available locally: {saved}. Failed: {failed}.");
            return;
        }

        CatalogCharacter? selected = catalog.Characters.FirstOrDefault(item =>
            string.Equals(item.Id, query, StringComparison.OrdinalIgnoreCase)
            || string.Equals(item.Name, query, StringComparison.OrdinalIgnoreCase));
        if (selected == null)
        {
            presenter.ShowLine("Character not found. Try ls or load.");
            return;
        }

        DownloadPortrait(selected);
    }

    private bool DownloadPortrait(CatalogCharacter character)
    {
        if (string.IsNullOrWhiteSpace(character.ImageUrl) &&
            string.IsNullOrWhiteSpace(character.ArtworkUrl))
        {
            presenter.ShowLine($"No portrait URL for {character.Name}.");
            return false;
        }

        string[] urls = new[] { character.ArtworkUrl, character.ImageUrl }
            .Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToArray();
        foreach (string url in urls)
        {
            try
            {
                string? existing = portraits.GetCachedPath(character.Id, url);
                string path = portraits.GetOrDownloadAsync(character.Id, url)
                    .GetAwaiter().GetResult();
                presenter.ShowLine($"{character.Name}: " + (existing == null
                    ? $"image saved to {path}" : $"using cached image {path}"));
                return true;
            }
            catch (Exception error) when (error is HttpRequestException or TaskCanceledException
                or IOException or UnauthorizedAccessException or ArgumentException
                or InvalidDataException)
            {
                presenter.ShowLine($"Image source unavailable ({url}): {error.Message}");
            }
        }
        return false;
    }

    private void SaveFetchedDetails()
    {
        try { catalog.Save(currentFile); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            presenter.ShowLine($"Details loaded, but could not update {currentFile}: {error.Message}");
        }
    }

    private void ShowExtra(CommandRequest request, Func<CatalogCharacter, string> render)
    {
        if (request.Arguments.Count == 0)
        {
            presenter.ShowLine("Specify a character ID or name.");
            return;
        }
        string query = string.Join(" ", request.Arguments);
        try
        {
            CatalogCharacter? item = catalog.FindAsync(query).GetAwaiter().GetResult();
            if (item == null)
            {
                presenter.ShowLine("Character not found. Use update and ls.");
                return;
            }
            SaveFetchedDetails();
            presenter.ShowLine(render(item));
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException
            or System.Text.Json.JsonException or ArgumentException)
        {
            presenter.ShowLine($"API unavailable: {error.Message}");
            CatalogCharacter? cached = catalog.Lookup(query);
            if (cached != null) presenter.ShowLine(render(cached));
        }
    }

    private void Weapons(CommandRequest request)
    {
        try
        {
            var weapons = catalog.GetWeaponSummariesAsync().GetAwaiter().GetResult();
            string filter = string.Join(" ", request.Arguments);
            var filtered = weapons.Where(x => string.IsNullOrWhiteSpace(filter)
                || x.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || x.Id.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var weapon in filtered.Take(100))
                presenter.ShowLine($"{weapon.Name} (id: {weapon.Id}) | {weapon.Weapon} | {weapon.Rarity} stars");
            presenter.ShowLine($"Matches: {filtered.Count} (showing first {Math.Min(100, filtered.Count)}).");
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException
            or System.Text.Json.JsonException)
        {
            presenter.ShowLine($"Cannot fetch Ambr weapons: {error.Message}");
        }
    }

    private void Weapon(CommandRequest request)
    {
        if (request.Arguments.Count == 0)
        {
            presenter.ShowLine("Usage: weapon <numeric ID or exact name>");
            return;
        }
        try
        {
            var x = catalog.GetWeaponAsync(string.Join(" ", request.Arguments))
                .GetAwaiter().GetResult();
            if (x == null) { presenter.ShowLine("Weapon not found."); return; }
            presenter.ShowLine($"Name: {x.Name} | ID: {x.Id} | {x.Rarity} stars");
            presenter.ShowLine($"Type: {x.Type} | Base ATK (level 1): {x.BaseAttack}");
            presenter.ShowLine($"Additional base stat: {x.SubStat}");
            presenter.ShowLine($"Passive: {x.PassiveName} - {x.PassiveDesc}");
            presenter.ShowLine($"Description: {x.Description}");
            presenter.ShowLine($"Icon: {x.IconUrl}");
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException
            or System.Text.Json.JsonException or ArgumentException)
        {
            presenter.ShowLine($"Cannot fetch Ambr weapon: {error.Message}");
        }
    }

    private void Create()
    {
        try
        {
            presenter.ShowLine("Create a character:");
            string name = ReadRequired("Name: ");
            string description = ReadRequired("Description: ");
            string element = ReadRequired("Element: ");
            string weapon = ReadRequired("Weapon: ");
            int rarity = ReadRarity();
            string imageUrl = ReadImageUrl();

            CatalogCharacter character = catalog.CreateCustom(
                name, description, element, weapon, rarity, imageUrl);

            catalog.Save(currentFile);
            presenter.ShowLine($"Created {character.Name} (id: {character.Id}).");
            presenter.ShowLine($"Automatically saved to {currentFile}.");
        }
        catch (OperationCanceledException)
        {
            presenter.ShowLine("Creation cancelled.");
        }
        catch (ArgumentException error)
        {
            presenter.ShowLine(error.Message);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            presenter.ShowLine($"Character added, but saving failed: {error.Message}");
            presenter.ShowLine("Use save to retry.");
        }
    }

    private void Save(CommandRequest request)
    {
        string file = request.Arguments.FirstOrDefault() ?? currentFile;
        try
        {
            catalog.Save(file);
            currentFile = file;
            presenter.ShowLine($"Catalog saved to {file}.");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            presenter.ShowLine($"Cannot save catalog: {error.Message}");
        }
    }

    private void Load(CommandRequest request)
    {
        string file = request.Arguments.FirstOrDefault() ?? currentFile;
        try
        {
            if (catalog.Load(file))
            {
                currentFile = file;
                presenter.ShowLine($"Catalog loaded from {file}.");
            }
            else
            {
                presenter.ShowLine("File not found.");
            }
        }
        catch (Exception error) when (error is IOException
            or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            presenter.ShowLine($"Cannot load catalog: {error.Message}");
        }
    }

    private string ReadRequired(string label)
    {
        while (true)
        {
            presenter.Show(label);
            string? value = presenter.Read();

            if (value == null || value.Equals("cancel", StringComparison.OrdinalIgnoreCase))
            {
                throw new OperationCanceledException();
            }

            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }

            presenter.ShowLine("This field cannot be empty (or type cancel).");
        }
    }

    private int ReadRarity()
    {
        while (true)
        {
            string value = ReadRequired("Rarity (1-5): ");
            if (int.TryParse(value, out int rarity) && rarity >= 1 && rarity <= 5)
            {
                return rarity;
            }

            presenter.ShowLine("Enter a number from 1 to 5.");
        }
    }

    private string ReadImageUrl()
    {
        while (true)
        {
            presenter.Show("Image URL (optional): ");
            string? value = presenter.Read();

            if (value == null || value.Equals("cancel", StringComparison.OrdinalIgnoreCase))
            {
                throw new OperationCanceledException();
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                return "";
            }

            if (Uri.TryCreate(value.Trim(), UriKind.Absolute, out Uri? url)
                && (url.Scheme == Uri.UriSchemeHttps || url.Scheme == Uri.UriSchemeHttp))
            {
                return url.ToString();
            }

            presenter.ShowLine("Enter a full http(s) URL or leave blank.");
        }
    }
}
