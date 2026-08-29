namespace GameSim;

/// <summary>
/// The immutable template for a kind of unit — what it <em>is</em>, never what it
/// is currently doing.
/// <para>
/// One definition is shared by every unit of that kind, so nothing in the
/// simulation may write to it. The mutable per-battle state — current HP,
/// position, attack cooldown — belongs on the battle instance instead.
/// </para>
/// </summary>
public sealed record UnitDefinition
{
    /// <summary>
    /// Damage variance applied unless a unit is deliberately tuned otherwise.
    /// </summary>
    public const double DefaultDamageVariance = 0.15;

    /// <summary>
    /// Creates a definition, rejecting any stat that could not describe a real
    /// unit. These are programmer errors — a catalog is written by hand, not fed
    /// from user input — so they throw rather than returning a failure.
    /// </summary>
    public UnitDefinition(
        string id,
        string displayName,
        int maxHp,
        int attackDamage,
        int attackIntervalTicks,
        int movementTicksPerHex,
        int attackRangeHexes,
        double damageVariance = DefaultDamageVariance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        // Thrown by hand rather than via ThrowIfNullOrWhiteSpace so the message
        // names the unit like every other failure here. The BCL helper names
        // only the parameter, which tells you a displayName was blank but not
        // which of six roster entries to go and look at.
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
    }

    /// <summary>Stable identifier, unique within a catalog. Not shown to players.</summary>
    public string Id { get; }

    /// <summary>Player-facing name.</summary>
    public string DisplayName { get; }

    /// <summary>Starting and maximum hit points.</summary>
    public int MaxHp { get; }

    /// <summary>Base damage per attack, before variance is rolled.</summary>
    public int AttackDamage { get; }

    /// <summary>Ticks between consecutive attacks. Higher is slower.</summary>
    public int AttackIntervalTicks { get; }

    /// <summary>Ticks to cross one hex. Higher is slower — this is a cost, not a speed.</summary>
    public int MovementTicksPerHex { get; }

    /// <summary>
    /// Reach as a hex distance to the target, so 1 means an adjacent hex and is
    /// the shortest legal reach — a unit that could attack nothing at all is not
    /// a unit. Melee and ranged are not kinds here, only ends of this number:
    /// something that later grants +1 range to a melee unit needs no new
    /// concept, and no code may treat "melee" as a category.
    /// </summary>
    public int AttackRangeHexes { get; }

    /// <summary>
    /// Damage spread as a fraction of <see cref="AttackDamage"/> — 0.15 means
    /// ±15%. Stored as a fraction for legibility; the roll itself must be taken
    /// in integer maths so a battle stays byte-identical across runtimes.
    /// </summary>
    public double DamageVariance { get; }

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
