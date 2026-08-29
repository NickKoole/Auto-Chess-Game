using System.Reflection;
using GameSim;

namespace GameSim.Tests;

public sealed class UnitDefinitionTests
{
    /// <summary>
    /// A valid definition, with one field overridable per test. Building the
    /// invalid cases by mutating a known-good baseline keeps each test about the
    /// single rule it is exercising.
    /// </summary>
    private static UnitDefinition Build(
        string id = "archer",
        string displayName = "Archer",
        int maxHp = 70,
        int attackDamage = 14,
        int attackIntervalTicks = 14,
        int movementTicksPerHex = 6,
        int attackRangeHexes = 3,
        double damageVariance = 0.15) =>
        new(id, displayName, maxHp, attackDamage, attackIntervalTicks,
            movementTicksPerHex, attackRangeHexes, damageVariance);

    [Fact]
    public void Keeps_every_stat_it_was_given()
    {
        var unit = new UnitDefinition(
            id: "knight",
            displayName: "Knight",
            maxHp: 180,
            attackDamage: 16,
            attackIntervalTicks: 17,
            movementTicksPerHex: 8,
            attackRangeHexes: 1,
            damageVariance: 0.2);

        Assert.Equal("knight", unit.Id);
        Assert.Equal("Knight", unit.DisplayName);
        Assert.Equal(180, unit.MaxHp);
        Assert.Equal(16, unit.AttackDamage);
        Assert.Equal(17, unit.AttackIntervalTicks);
        Assert.Equal(8, unit.MovementTicksPerHex);
        Assert.Equal(1, unit.AttackRangeHexes);
        Assert.Equal(0.2, unit.DamageVariance);
    }

    [Fact]
    public void Damage_variance_defaults_to_fifteen_percent()
    {
        var unit = new UnitDefinition(
            id: "militia",
            displayName: "Militia",
            maxHp: 100,
            attackDamage: 12,
            attackIntervalTicks: 12,
            movementTicksPerHex: 6,
            attackRangeHexes: 1);

        Assert.Equal(UnitDefinition.DefaultDamageVariance, unit.DamageVariance);
        Assert.Equal(0.15, unit.DamageVariance);
    }

    [Fact]
    public void Exposes_no_public_setters()
    {
        // The definition is shared by every unit of its kind, so a setter would
        // let one battle rewrite another's stats.
        string[] settable =
        [
            .. typeof(UnitDefinition)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.SetMethod is { IsPublic: true })
                .Select(p => p.Name)
        ];

        Assert.Empty(settable);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Rejects_non_positive_max_hp(int maxHp)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Build(maxHp: maxHp));
        Assert.Contains("archer", ex.Message, StringComparison.Ordinal);
        Assert.Contains("maxHp", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Rejects_non_positive_attack_damage(int damage)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Build(attackDamage: damage));
        Assert.Contains("archer", ex.Message, StringComparison.Ordinal);
        Assert.Contains("attackDamage", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Rejects_non_positive_attack_interval(int interval)
    {
        // A zero interval would mean infinite attacks in a single tick.
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Build(attackIntervalTicks: interval));
        Assert.Contains("archer", ex.Message, StringComparison.Ordinal);
        Assert.Contains("attackIntervalTicks", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public void Rejects_non_positive_movement_cost(int ticksPerHex)
    {
        // Zero ticks per hex would let a unit cross the board instantly.
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Build(movementTicksPerHex: ticksPerHex));
        Assert.Contains("archer", ex.Message, StringComparison.Ordinal);
        Assert.Contains("movementTicksPerHex", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-10)]
    public void Rejects_negative_attack_range(int range)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Build(attackRangeHexes: range));
        Assert.Contains("archer", ex.Message, StringComparison.Ordinal);
        Assert.Contains("attackRangeHexes", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Accepts_zero_attack_range()
    {
        // Only negative range is invalid. Zero is reserved for a unit that
        // cannot attack at all, which the ticket does not forbid.
        UnitDefinition unit = Build(attackRangeHexes: 0);

        Assert.Equal(0, unit.AttackRangeHexes);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(-1.0)]
    [InlineData(1.01)]
    [InlineData(2.0)]
    public void Rejects_damage_variance_outside_zero_to_one(double variance)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Build(damageVariance: variance));
        Assert.Contains("archer", ex.Message, StringComparison.Ordinal);
        Assert.Contains("damageVariance", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Rejects_damage_variance_that_is_not_a_real_number(double variance)
    {
        // NaN is the one that bites: every comparison against it is false, so a
        // `variance < 0 || variance > 1` guard would wave it straight through
        // and every damage roll from that unit would come out NaN.
        Assert.Throws<ArgumentOutOfRangeException>(() => Build(damageVariance: variance));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(1.0)]
    public void Accepts_damage_variance_at_both_bounds(double variance)
    {
        UnitDefinition unit = Build(damageVariance: variance);

        Assert.Equal(variance, unit.DamageVariance);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_a_missing_id(string? id)
    {
        // ThrowsAny, not Throws: null yields ArgumentNullException and blank
        // yields ArgumentException, and pinning the exact subtype would be
        // asserting which BCL helper was called rather than that the id was
        // rejected.
        Assert.ThrowsAny<ArgumentException>(() => Build(id: id!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_a_missing_display_name(string? displayName)
    {
        Assert.ThrowsAny<ArgumentException>(() => Build(displayName: displayName!));
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(4, false)]
    public void Reports_melee_by_reach(int range, bool expected)
    {
        Assert.Equal(expected, Build(attackRangeHexes: range).IsMelee);
    }
}
