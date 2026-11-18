using Practical1.GameSystem;

namespace Practical1.Infra.Remote;

public class CharacterMapper
{
    private readonly Game game;

    public CharacterMapper(
        Game game)
    {
        this.game = game;
    }

    public Character Map(
        RemoteCharacterData data)
    {
        int health = StatOr(data, "HP", 100 + data.Rarity * 20);
        int armor = StatOr(data, "DEF", data.Rarity * 2);
        int attackPower = StatOr(data, "ATK", 20 + data.Rarity * 5);

        Character character;

        if (data.Weapon == "Catalyst")
        {
            character =
                new Mage(
                    data.Name,
                    health,
                    armor,
                    attackPower
                );
        }
        else
        {
            character =
                new Warrior(
                    data.Name,
                    health,
                    armor,
                    attackPower
                );
        }

        character.Description =
            data.Description;

        character.Rarity =
            data.Rarity;

        character.Vision =
            data.Vision;

        character.WeaponType =
            data.Weapon;

        foreach (
            RemoteTalentData talent
            in data.SkillTalents)
        {
            Ability ability =
                new Ability(
                    talent.Name,
                    data.Rarity * 5,
                    talent.Description
                );

            game.AddAbility(
                character,
                ability
            );
        }

        return character;
    }

    private static int StatOr(RemoteCharacterData data, string name, int fallback)
    {
        AmbrStat? stat = data.BaseStats.FirstOrDefault(s =>
            s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        return stat == null || !double.IsFinite(stat.BaseValue) || stat.BaseValue <= 0
            ? fallback : Math.Max(1, (int)Math.Round(stat.BaseValue));
    }
}