namespace Practical1.GameSystem;

public sealed class BattleFighter
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Type { get; set; } = "warrior";
    public int MaxHealth { get; set; }
    public int Health { get; set; }
    public int Damage { get; set; }
    public int Armor { get; set; }
    public int Resist { get; set; }
    public int Speed { get; set; }
    public bool Defending { get; set; }

    public BattleFighter Copy() => (BattleFighter)MemberwiseClone();
}

public sealed class BattleSaveData
{
    public List<BattleFighter> Fighters { get; set; } = new();
    public List<string> History { get; set; } = new();
    public List<string> TurnOrder { get; set; } = new();
    public int TurnIndex { get; set; }
    public int Round { get; set; }
    public bool Started { get; set; }
    public bool Finished { get; set; }
}

public sealed class TurnBasedBattle
{
    private readonly List<BattleFighter> fighters = new();
    private readonly List<string> history = new();
    private List<string> order = new();
    private int turnIndex;

    public int Round { get; private set; }
    public bool Started { get; private set; }
    public bool Finished { get; private set; }
    public IReadOnlyList<string> History => history.AsReadOnly();
    public IReadOnlyList<BattleFighter> Fighters => fighters.Select(x => x.Copy()).ToList().AsReadOnly();
    public BattleFighter? Current => !Started || Finished || turnIndex >= order.Count
        ? null : GetFighter(order[turnIndex]).Copy();

    public void Reset()
    {
        fighters.Clear();
        history.Clear();
        order.Clear();
        turnIndex = 0;
        Round = 0;
        Started = false;
        Finished = false;
    }

    public static void Validate(BattleFighter fighter)
    {
        ArgumentNullException.ThrowIfNull(fighter);
        if (string.IsNullOrWhiteSpace(fighter.Id) || string.IsNullOrWhiteSpace(fighter.Name))
            throw new ArgumentException("Character ID and name cannot be empty.");
        if (!string.Equals(fighter.Type, "warrior", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(fighter.Type, "mage", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Type must be warrior or mage.");
        if (fighter.MaxHealth <= 0 || fighter.Health < 0 || fighter.Health > fighter.MaxHealth
            || fighter.Damage < 0 || fighter.Armor < 0 || fighter.Resist < 0 || fighter.Resist > 100
            || fighter.Speed < 1 || fighter.Speed > 300)
            throw new ArgumentException("Invalid character stats (Resist: 0-100; Speed: 1-300).");
    }

    public void Add(BattleFighter fighter)
    {
        if (Started) throw new InvalidOperationException("Reset battle before adding characters.");
        Validate(fighter);
        if (fighters.Any(x => x.Id.Equals(fighter.Id, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Character with this ID already exists.");
        fighters.Add(fighter.Copy());
    }

    public void Start()
    {
        if (Started) throw new InvalidOperationException("Battle already started.");
        if (fighters.Count(x => x.Health > 0) < 2)
            throw new InvalidOperationException("Add at least two living characters.");
        Started = true;
        Finished = false;
        Round = 0;
        BeginRound();
    }

    public void Attack(string targetId) => ApplyAttack(targetId, false);
    public void Magic(string targetId) => ApplyAttack(targetId, true);

    public static int CalculateDamage(BattleFighter attacker, BattleFighter target, bool magical = false)
    {
        Validate(attacker);
        Validate(target);
        double raw = magical ? attacker.Damage : Math.Max(0, attacker.Damage - target.Armor);
        double resist = magical ? Math.Min(95.0, target.Resist * 1.5) : target.Resist;
        double damage = raw * (1 - resist / 100.0);
        if (target.Defending) damage *= 0.5;
        return Math.Max(0, (int)Math.Floor(damage));
    }

    private void ApplyAttack(string targetId, bool magical)
    {
        BattleFighter actor = Active();
        if (magical && !actor.Type.Equals("mage", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only mages can use magic.");
        BattleFighter target = GetFighter(targetId);
        if (ReferenceEquals(actor, target)) throw new InvalidOperationException("Cannot attack yourself.");
        if (target.Health == 0) throw new InvalidOperationException("Target has already been defeated.");
        int damage = CalculateDamage(actor, target, magical);
        target.Health = Math.Max(0, target.Health - damage);
        target.Defending = false;
        Record($"{actor.Name} {(magical ? "casts a spell on" : "attacks")} {target.Name}: -{damage} HP ({target.Health}/{target.MaxHealth}).");
        Advance();
    }

    public void Defend()
    {
        BattleFighter actor = Active();
        actor.Defending = true;
        Record($"{actor.Name} defends (next damage is halved).");
        Advance();
    }

    public void Heal(int amount)
    {
        BattleFighter actor = Active();
        if (amount <= 0 || amount > 30) throw new ArgumentException("Healing must be between 1 and 30.");
        int before = actor.Health;
        actor.Health = Math.Min(actor.MaxHealth, actor.Health + amount);
        Record($"{actor.Name} heals +{actor.Health - before} HP ({actor.Health}/{actor.MaxHealth}).");
        Advance();
    }

    private BattleFighter Active()
    {
        if (!Started || Finished) throw new InvalidOperationException("Start a battle first.");
        if (turnIndex >= order.Count) throw new InvalidOperationException("No active turn.");
        return GetFighter(order[turnIndex]);
    }

    private BattleFighter GetFighter(string id) =>
        fighters.FirstOrDefault(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"Character not found: {id}");

    private void Record(string message) => history.Add($"[Round {Round}] {message}");

    private void BeginRound()
    {
        Round++;
        order = fighters.Where(x => x.Health > 0)
            .SelectMany(x => Enumerable.Range(1, Math.Max(1, x.Speed / 100))
                .Select(n => new { x.Id, x.Speed, Time = (double)n / x.Speed }))
            .OrderBy(x => x.Time).ThenByDescending(x => x.Speed)
            .ThenBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.Id).ToList();
        turnIndex = 0;
        Record("New round. Turn order: " + string.Join(", ", order));
    }

    private void Advance()
    {
        if (fighters.Count(x => x.Health > 0) <= 1)
        {
            Finished = true;
            Record("Battle finished. Winner: " + fighters.First(x => x.Health > 0).Name);
            return;
        }
        do
        {
            turnIndex++;
            if (turnIndex >= order.Count) BeginRound();
        } while (GetFighter(order[turnIndex]).Health == 0);
    }

    public BattleSaveData Save() => new()
    {
        Fighters = fighters.Select(x => x.Copy()).ToList(),
        History = new List<string>(history),
        TurnOrder = new List<string>(order),
        TurnIndex = turnIndex,
        Round = Round,
        Started = Started,
        Finished = Finished
    };

    public void Load(BattleSaveData state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Fighters is null || state.History is null || state.TurnOrder is null)
            throw new ArgumentException("Invalid save file.");
        foreach (var fighter in state.Fighters) Validate(fighter);
        if (state.Fighters.Select(x => x.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != state.Fighters.Count)
            throw new ArgumentException("Duplicate character IDs in save.");
        if (state.Round < 0 || state.TurnIndex < 0 || state.TurnIndex > state.TurnOrder.Count
            || state.TurnOrder.Any(id => !state.Fighters.Any(f => f.Id.Equals(id, StringComparison.OrdinalIgnoreCase))))
            throw new ArgumentException("Invalid turn order in save.");
        if (state.Started && (state.Round < 1 || state.TurnOrder.Count == 0 || (!state.Finished && state.TurnIndex >= state.TurnOrder.Count)))
            throw new ArgumentException("Invalid battle state.");
        fighters.Clear();
        fighters.AddRange(state.Fighters.Select(x => x.Copy()));
        history.Clear();
        history.AddRange(state.History);
        order = new List<string>(state.TurnOrder);
        turnIndex = state.TurnIndex;
        Round = state.Round;
        Started = state.Started;
        Finished = state.Finished;
    }
}
