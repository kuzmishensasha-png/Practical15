using System.Text.Json;
using Practical1.GameSystem;

namespace Practical1.CLI.Battle;

public sealed class BattleCommands
{
    private readonly TurnBasedBattle battle = new();
    private readonly IPresenter presenter;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public BattleCommands(IPresenter presenter) => this.presenter = presenter;

    public void Register(CommandInterpreter interpreter)
    {
        interpreter.Register("help", _ => Help());
        interpreter.Register("status", _ => Status());
        interpreter.Register("turn", _ => Turn());
        interpreter.Register("history", _ => History());
        interpreter.Register("reset", _ => { battle.Reset(); presenter.ShowLine("Battle reset."); });
        interpreter.Register("start", _ => Execute(() => { battle.Start(); Status(); }));
        interpreter.Register("defend", _ => Execute(() => { battle.Defend(); Turn(); }));
        interpreter.Register("attack", r => Execute(() => { battle.Attack(Arg(r, 0, "attack <target_id>")); Turn(); }));
        interpreter.Register("magic", r => Execute(() => { battle.Magic(Arg(r, 0, "magic <target_id>")); Turn(); }));
        interpreter.Register("heal", r => Execute(() => { battle.Heal(int.Parse(Arg(r, 0, "heal <1..30>"))); Turn(); }));
        interpreter.Register("add", r => Execute(() => Add(r)));
        interpreter.Register("loadchars", r => Execute(() => ImportCharacters(Arg(r, 0, "loadchars <file>"))));
        interpreter.Register("save", r => Execute(() => Save(r.Arguments.FirstOrDefault() ?? "battle-save.json")));
        interpreter.Register("load", r => Execute(() => Load(r.Arguments.FirstOrDefault() ?? "battle-save.json")));
    }

    private void Execute(Action action)
    {
        try { action(); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException or JsonException or UnauthorizedAccessException or OverflowException or FormatException)
        { presenter.ShowLine("Error: " + ex.Message); }
    }

    private static string Arg(CommandRequest r, int index, string help) =>
        r.Arguments.Count > index ? r.Arguments[index] : throw new ArgumentException("Usage: " + help);

    private void Add(CommandRequest request)
    {
        if (request.Arguments.Count != 7)
            throw new ArgumentException("Usage: add <id> <warrior|mage> <hp> <damage> <armor> <resist> <speed>");
        var a = request.Arguments;
        int hp = int.Parse(a[2]);
        battle.Add(new BattleFighter
        {
            Id = a[0], Name = a[0], Type = a[1], MaxHealth = hp, Health = hp,
            Damage = int.Parse(a[3]), Armor = int.Parse(a[4]), Resist = int.Parse(a[5]), Speed = int.Parse(a[6])
        });
        presenter.ShowLine("Added: " + a[0]);
    }

    private void ImportCharacters(string path)
    {
        var imported = JsonSerializer.Deserialize<List<BattleFighter>>(File.ReadAllText(path), JsonOptions)
            ?? throw new ArgumentException("Empty character file.");
        if (battle.Started) throw new InvalidOperationException("Reset battle before importing characters.");
        var existing = battle.Fighters.Select(x => x.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var fighter in imported)
        {
            TurnBasedBattle.Validate(fighter);
            if (!existing.Add(fighter.Id)) throw new ArgumentException("Duplicate character ID: " + fighter.Id);
        }
        foreach (var fighter in imported) battle.Add(fighter);
        presenter.ShowLine($"Imported {imported.Count} characters.");
    }

    private void Save(string path)
    {
        File.WriteAllText(path, JsonSerializer.Serialize(battle.Save(), JsonOptions));
        presenter.ShowLine("Battle saved: " + path);
    }

    private void Load(string path)
    {
        BattleSaveData state = JsonSerializer.Deserialize<BattleSaveData>(File.ReadAllText(path), JsonOptions)
            ?? throw new ArgumentException("Empty save file.");
        battle.Load(state);
        presenter.ShowLine("Battle loaded: " + path);
        Status();
    }

    private void Status()
    {
        presenter.ShowLine($"Round: {battle.Round}  Started: {battle.Started}  Finished: {battle.Finished}");
        foreach (var c in battle.Fighters)
            presenter.ShowLine($"{c.Id}: {c.Health}/{c.MaxHealth} HP, {c.Type}, Damage {c.Damage}, Armor {c.Armor}, Resist {c.Resist}%, Speed {c.Speed}");
        Turn();
    }

    private void Turn() => presenter.ShowLine(battle.Current is { } f
        ? $"Current turn: {f.Name} ({f.Id}), round {battle.Round}"
        : battle.Finished ? "Battle finished. Check history." : "No active turn. Use start.");

    private void History()
    {
        if (battle.History.Count == 0) presenter.ShowLine("History is empty.");
        foreach (string entry in battle.History) presenter.ShowLine(entry);
    }

    private void Help()
    {
        foreach (string line in new[]
        {
            "Turn-based game commands:",
            "loadchars Samples/battle-characters.json   - import fighters",
            "add <id> <warrior|mage> <hp> <damage> <armor> <resist> <speed>",
            "start / turn / status / history",
            "attack <target_id> / magic <target_id> / defend / heal <1..30>",
            "save [file] / load [file] / reset / exit"
        }) presenter.ShowLine(line);
    }
}
