namespace GameSim;

/// <summary>
/// The built-in unit roster, written in code.
/// <para>
/// Stats are tuned for a small board — 8 rows deep with a 2-row neutral gap —
/// so armies close within a handful of ticks. Read the movement costs and
/// ranges against that, not against a large field.
/// </para>
/// </summary>
public sealed class StaticUnitCatalog : IUnitCatalog
{
    private readonly Dictionary<string, UnitDefinition> _byId;

    /// <summary>Creates a catalog holding the built-in roster.</summary>
    public StaticUnitCatalog()
        : this(BuiltIn())
    {
    }

    /// <summary>
    /// Creates a catalog from an explicit set of definitions, rejecting
    /// duplicate ids. A duplicate is a mistake in the roster, and silently
    /// keeping the last one would hide it until a battle used the wrong stats.
    /// </summary>
    public StaticUnitCatalog(IReadOnlyList<UnitDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        _byId = new Dictionary<string, UnitDefinition>(definitions.Count, StringComparer.Ordinal);

        foreach (UnitDefinition definition in definitions)
        {
            ArgumentNullException.ThrowIfNull(definition);

            if (!_byId.TryAdd(definition.Id, definition))
            {
                throw new ArgumentException(
                    $"Duplicate unit id '{definition.Id}' in the catalog. Ids must be unique.",
                    nameof(definitions));
            }
        }

        // Source order, captured once. The dictionary above is only ever used
        // for lookup, never for enumeration.
        All = [.. definitions];
    }

    /// <inheritdoc />
    public IReadOnlyList<UnitDefinition> All { get; }

    /// <inheritdoc />
    public UnitDefinition Get(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        if (_byId.TryGetValue(id, out UnitDefinition? definition))
        {
            return definition;
        }

        throw new KeyNotFoundException(
            $"No unit definition with id '{id}'. Known ids: {string.Join(", ", All.Select(d => d.Id))}.");
    }

    /// <summary>
    /// The built-in roster: three melee, three ranged.
    /// </summary>
    private static UnitDefinition[] BuiltIn() =>
    [
        // --- melee, range 1 -------------------------------------------------
        new(
            id: "militia",
            displayName: "Militia",
            maxHp: 100,
            attackDamage: 12,
            attackIntervalTicks: 12,
            movementTicksPerHex: 6,
            attackRangeHexes: 1),

        new(
            id: "knight",
            displayName: "Knight",
            maxHp: 180,
            attackDamage: 16,
            attackIntervalTicks: 16,
            movementTicksPerHex: 8,
            attackRangeHexes: 1),

        new(
            id: "berserker",
            displayName: "Berserker",
            maxHp: 90,
            attackDamage: 10,
            attackIntervalTicks: 7,
            movementTicksPerHex: 5,
            attackRangeHexes: 1),

        // --- ranged, range > 1 ----------------------------------------------
        new(
            id: "archer",
            displayName: "Archer",
            maxHp: 70,
            attackDamage: 14,
            attackIntervalTicks: 14,
            movementTicksPerHex: 6,
            attackRangeHexes: 3),

        // Deliberately swingy: a heavy, slow shot that sometimes falls short of
        // killing what it should.
        new(
            id: "crossbowman",
            displayName: "Crossbowman",
            maxHp: 85,
            attackDamage: 26,
            attackIntervalTicks: 24,
            movementTicksPerHex: 7,
            attackRangeHexes: 4,
            damageVariance: 0.25),

        // Deliberately reliable: fragile, but you can count on the number.
        new(
            id: "mage",
            displayName: "Mage",
            maxHp: 60,
            attackDamage: 22,
            attackIntervalTicks: 20,
            movementTicksPerHex: 7,
            attackRangeHexes: 3,
            damageVariance: 0.05),
    ];
}
