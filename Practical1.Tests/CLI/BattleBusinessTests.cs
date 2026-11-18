using Practical1.GameSystem;

namespace Practical1.Tests.CLI;

public class BattleBusinessTests
{
    private static BattleFighter Fighter(string id, int speed = 100) => new()
    {
        Id = id, Name = id, Type = "warrior", MaxHealth = 100, Health = 100,
        Damage = 20, Armor = 5, Resist = 20, Speed = speed
    };

    [Fact]
    public void PhysicalDamageAppliesArmorAndResist()
    {
        Assert.Equal(12, TurnBasedBattle.CalculateDamage(Fighter("a"), Fighter("b")));
    }

    [Fact]
    public void DefendHalvesNextDamage()
    {
        var b = Fighter("b");
        b.Defending = true;
        Assert.Equal(6, TurnBasedBattle.CalculateDamage(Fighter("a"), b));
    }

    [Fact]
    public void MagicIgnoresArmorButUsesStrongerResist()
    {
        var a = Fighter("a");
        var b = Fighter("b");
        b.Armor = 100;
        Assert.Equal(14, TurnBasedBattle.CalculateDamage(a, b, magical: true));
    }

    [Fact]
    public void FasterFighterActsFirstAndCanActTwice()
    {
        var battle = new TurnBasedBattle();
        battle.Add(Fighter("slow", 100));
        battle.Add(Fighter("fast", 200));
        battle.Start();
        Assert.Equal("fast", battle.Current?.Id);
        battle.Defend();
        Assert.Equal("fast", battle.Current?.Id);
        battle.Defend();
        Assert.Equal("slow", battle.Current?.Id);
    }

    [Fact]
    public void SaveAndLoadPreserveTurnAndHistory()
    {
        var battle = new TurnBasedBattle();
        battle.Add(Fighter("one", 100));
        battle.Add(Fighter("two", 150));
        battle.Start();
        battle.Attack("one");
        BattleSaveData save = battle.Save();
        var loaded = new TurnBasedBattle();
        loaded.Load(save);
        Assert.Equal(battle.Round, loaded.Round);
        Assert.Equal(battle.Current?.Id, loaded.Current?.Id);
        Assert.Equal(battle.History.Count, loaded.History.Count);
        Assert.Equal(battle.Fighters.First(x => x.Id == "one").Health,
            loaded.Fighters.First(x => x.Id == "one").Health);
    }

    [Fact]
    public void BadStatsAreRejected()
    {
        var invalid = Fighter("bad");
        invalid.Resist = 110;
        Assert.Throws<ArgumentException>(() => new TurnBasedBattle().Add(invalid));
    }
}
