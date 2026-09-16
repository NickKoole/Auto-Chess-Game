namespace GameSim;

/// <summary>A unit's single attack type - every unit has exactly one, never a set.</summary>
public enum DamageType
{
    Slashing,
    Blunt,
    Piercing,
    Fire,
    Frost,
    Nature,
    Shadow,
}

/// <summary>What a kind of unit is, never what it is currently doing - the mutable per-battle state lives on <see cref="BattleUnit"/> instead.</summary>
public sealed record UnitDefinition
{
    public const double DefaultDamageVariance = 0.15;

    /// <summary>Rejects any stat that couldn't describe a real unit - a catalog is written by hand, so these are programmer errors, not API-boundary failures.</summary>
    public UnitDefinition(
        string id,
        string displayName,
        int maxHp,
        int attackDamage,
        int attackIntervalTicks,
        int movementTicksPerHex,
        int attackRangeHexes,
        bool playerSelectable,
        double damageVariance = DefaultDamageVariance,
        DamageType damageType = DamageType.Slashing,
        int slashingResist = 0,
        int bluntResist = 0,
        int piercingResist = 0,
        int fireResist = 0,
        int frostResist = 0,
        int natureResist = 0,
        int shadowResist = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        // Thrown by hand, not via ThrowIfNullOrWhiteSpace, so the message names
        // the unit - the BCL helper only names the parameter.
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException(
                $"Unit '{id}': displayName must not be null or blank.",
                nameof(displayName));
        }

        Id = id;
        DisplayName = displayName;
        MaxHp = RequirePositive(maxHp, id, nameof(maxHp));
        AttackDamage = RequirePositive(attackDamage, id, nameof(attackDamage));
        AttackIntervalTicks = RequirePositive(attackIntervalTicks, id, nameof(attackIntervalTicks));
        MovementTicksPerHex = RequirePositive(movementTicksPerHex, id, nameof(movementTicksPerHex));
        AttackRangeHexes = RequirePositive(attackRangeHexes, id, nameof(attackRangeHexes));
        PlayerSelectable = playerSelectable;

        // Written as a positive assertion rather than `< 0 || > 1` so that NaN
        // is rejected too: every comparison against NaN is false, so the
        // negated form would let it through and poison the damage roll.
        if (!(damageVariance >= 0.0 && damageVariance <= 1.0))
        {
            throw new ArgumentOutOfRangeException(
                nameof(damageVariance),
                damageVariance,
                $"Unit '{id}': damageVariance must be between 0 and 1 inclusive, but was {damageVariance}.");
        }

        DamageVariance = damageVariance;
        DamageType = damageType;
        SlashingResist = slashingResist;
        BluntResist = bluntResist;
        PiercingResist = piercingResist;
        FireResist = fireResist;
        FrostResist = frostResist;
        NatureResist = natureResist;
        ShadowResist = shadowResist;
    }

    /// <summary>Unique within a catalog. Not shown to players.</summary>
    public string Id { get; }

    public string DisplayName { get; }

    /// <summary>Also the starting value - see <see cref="BattleUnit"/>'s constructor.</summary>
    public int MaxHp { get; }

    /// <summary>Before variance is rolled.</summary>
    public int AttackDamage { get; }

    /// <summary>Higher is slower.</summary>
    public int AttackIntervalTicks { get; }

    /// <summary>Higher is slower - this is a cost, not a speed.</summary>
    public int MovementTicksPerHex { get; }

    /// <summary>
    /// 1 means an adjacent hex, the shortest legal reach. Melee and ranged are
    /// not kinds here, only ends of this number - no code may treat "melee" as
    /// a category.
    /// </summary>
    public int AttackRangeHexes { get; }

    /// <summary>Whether a player can draft this unit into their own roster. False keeps it in the shared catalog for server-authored placements (enemy setups, matchups) without exposing it to a player's picker.</summary>
    public bool PlayerSelectable { get; }

    /// <summary>
    /// Fraction of <see cref="AttackDamage"/> - 0.15 means +/-15%. A
    /// <see langword="double"/> for legibility only; the roll itself is
    /// integer maths.
    /// </summary>
    public double DamageVariance { get; }

    /// <summary>What this unit's single attack deals.</summary>
    public DamageType DamageType { get; }

    /// <summary>Percentage resisted against an attack whose <see cref="DamageType"/> is <see cref="GameSim.DamageType.Slashing"/> - see <see cref="ResistFor"/> for how the cap applies.</summary>
    public int SlashingResist { get; }

    public int BluntResist { get; }

    public int PiercingResist { get; }

    public int FireResist { get; }

    public int FrostResist { get; }

    public int NatureResist { get; }

    public int ShadowResist { get; }

    /// <summary>This unit's resistance against <paramref name="damageType"/>, uncapped - <see cref="BattleSimulation"/> applies the 75% cap.</summary>
    public int ResistFor(DamageType damageType) => damageType switch
    {
        GameSim.DamageType.Slashing => SlashingResist,
        GameSim.DamageType.Blunt => BluntResist,
        GameSim.DamageType.Piercing => PiercingResist,
        GameSim.DamageType.Fire => FireResist,
        GameSim.DamageType.Frost => FrostResist,
        GameSim.DamageType.Nature => NatureResist,
        GameSim.DamageType.Shadow => ShadowResist,
        _ => throw new ArgumentOutOfRangeException(nameof(damageType), damageType, "Unknown damage type."),
    };

    private static int RequirePositive(int value, string id, string field)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(
                field,
                value,
                $"Unit '{id}': {field} must be positive, but was {value}.");
        }

        return value;
    }
}
