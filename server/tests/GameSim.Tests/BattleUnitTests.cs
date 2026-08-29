using GameSim;

namespace GameSim.Tests;

public sealed class BattleUnitTests
{
    private static UnitDefinition Definition(int maxHp = 180) =>
        new(
            id: "knight",
            displayName: "Knight",
            maxHp: maxHp,
            attackDamage: 16,
            attackIntervalTicks: 16,
            movementTicksPerHex: 8,
            attackRangeHexes: 1);

    [Fact]
    public void Starts_at_the_definitions_max_hp()
    {
        UnitDefinition definition = Definition(maxHp: 137);

        var unit = new BattleUnit(definition, BattleSide.Player, new HexCoord(6, 2));

        Assert.Equal(137, unit.CurrentHp);
        Assert.Equal(definition.MaxHp, unit.CurrentHp);
    }

    [Fact]
    public void Starts_ready_to_attack()
    {
        // Zero, not the attack interval: a unit that has just been deployed has
        // not swung yet, so making it wait a full interval would silently hand
        // the first hit of every battle to nobody.
        var unit = new BattleUnit(Definition(), BattleSide.Opponent, new HexCoord(1, 1));

        Assert.Equal(0, unit.AttackCooldownTicks);
    }

    [Fact]
    public void Keeps_the_state_it_was_built_with()
    {
        UnitDefinition definition = Definition();
        var position = new HexCoord(5, 3);

        var unit = new BattleUnit(definition, BattleSide.Player, position);

        // Same instance, not merely an equal one: one definition is shared by
        // every unit of its kind, and copying it per unit would let a future
        // buff write to one army's stats only.
        Assert.Same(definition, unit.Definition);
        Assert.Equal(BattleSide.Player, unit.Side);
        Assert.Equal(position, unit.Position);
    }

    [Fact]
    public void Rejects_a_missing_definition()
    {
        Assert.Throws<ArgumentNullException>(
            () => new BattleUnit(null!, BattleSide.Player, new HexCoord(5, 0)));
    }
}
