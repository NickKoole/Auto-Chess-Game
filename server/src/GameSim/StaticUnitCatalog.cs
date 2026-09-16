namespace GameSim;

/// <summary>Stats are tuned for an 8-row board with a 2-row neutral gap, so armies close within a handful of ticks - read movement costs and ranges against that.</summary>
public sealed class StaticUnitCatalog : IUnitCatalog
{
    private readonly Dictionary<string, UnitDefinition> _byId;

    public StaticUnitCatalog()
        : this(BuiltIn())
    {
    }

    /// <summary>Rejects duplicate ids - silently keeping the last one would hide a roster mistake until a battle used the wrong stats.</summary>
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

    public IReadOnlyList<UnitDefinition> All { get; }

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
        new(
            id: "conscript",
            displayName: "Conscript",
            maxHp: 100,
            attackDamage: 12,
            attackIntervalTicks: 12,
            movementTicksPerHex: 6,
            attackRangeHexes: 1,
            playerSelectable: true,
            damageType: DamageType.Slashing,
            slashingResist: 0, bluntResist: 0, piercingResist: 0,
            fireResist: 0, frostResist: 0, natureResist: 0, shadowResist: 0),

        new(
            id: "knight",
            displayName: "Knight",
            maxHp: 180,
            attackDamage: 16,
            attackIntervalTicks: 16,
            movementTicksPerHex: 8,
            attackRangeHexes: 1,
            playerSelectable: true,
            damageType: DamageType.Slashing,
            slashingResist: 0, bluntResist: 0, piercingResist: 0,
            fireResist: 0, frostResist: 0, natureResist: 0, shadowResist: 0),

        new(
            id: "berserker",
            displayName: "Berserker",
            maxHp: 90,
            attackDamage: 10,
            attackIntervalTicks: 7,
            movementTicksPerHex: 5,
            attackRangeHexes: 1,
            playerSelectable: true,
            damageType: DamageType.Slashing,
            slashingResist: 0, bluntResist: 0, piercingResist: 0,
            fireResist: 0, frostResist: 0, natureResist: 0, shadowResist: 0),

        new(
            id: "archer",
            displayName: "Archer",
            maxHp: 70,
            attackDamage: 14,
            attackIntervalTicks: 14,
            movementTicksPerHex: 6,
            attackRangeHexes: 3,
            playerSelectable: true,
            damageType: DamageType.Piercing,
            slashingResist: 0, bluntResist: 0, piercingResist: 0,
            fireResist: 0, frostResist: 0, natureResist: 0, shadowResist: 0),

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
            playerSelectable: true,
            damageVariance: 0.25,
            damageType: DamageType.Piercing,
            slashingResist: 0, bluntResist: 0, piercingResist: 0,
            fireResist: 0, frostResist: 0, natureResist: 0, shadowResist: 0),

        // Deliberately reliable: fragile, but you can count on the number.
        new(
            id: "hexcaster",
            displayName: "Hexcaster",
            maxHp: 60,
            attackDamage: 22,
            attackIntervalTicks: 20,
            movementTicksPerHex: 7,
            attackRangeHexes: 3,
            playerSelectable: true,
            damageVariance: 0.05,
            damageType: DamageType.Shadow,
            slashingResist: 0, bluntResist: 0, piercingResist: 0,
            fireResist: 0, frostResist: 0, natureResist: 0, shadowResist: 0),

        // Deliberately swingy: a zealot fighting with a burning weapon, the
        // roster's first melee unit that is also magical.
        new(
            id: "acolyte",
            displayName: "Acolyte",
            maxHp: 75,
            attackDamage: 18,
            attackIntervalTicks: 14,
            movementTicksPerHex: 6,
            attackRangeHexes: 1,
            playerSelectable: true,
            damageVariance: 0.20,
            damageType: DamageType.Fire,
            slashingResist: 0, bluntResist: 0, piercingResist: 0,
            fireResist: 0, frostResist: 0, natureResist: 0, shadowResist: 0),

        // Enemy-only monsters below - never drafted by a player, only placed
        // by a hand-authored enemy setup or matchup.
        new(
            id: "goblin",
            displayName: "Goblin",
            maxHp: 55,
            attackDamage: 8,
            attackIntervalTicks: 9,
            movementTicksPerHex: 4,
            attackRangeHexes: 1,
            playerSelectable: false,
            damageType: DamageType.Blunt,
            slashingResist: 0, bluntResist: 0, piercingResist: 0,
            fireResist: 0, frostResist: 0, natureResist: 0, shadowResist: 0),

        // Deliberately swingy: a feral, unpredictable bite.
        new(
            id: "dire-wolf",
            displayName: "Dire Wolf",
            maxHp: 70,
            attackDamage: 13,
            attackIntervalTicks: 8,
            movementTicksPerHex: 3,
            attackRangeHexes: 1,
            playerSelectable: false,
            damageVariance: 0.30,
            damageType: DamageType.Piercing,
            slashingResist: 0, bluntResist: 0, piercingResist: 0,
            fireResist: 0, frostResist: 0, natureResist: 0, shadowResist: 0),

        new(
            id: "ogre",
            displayName: "Ogre",
            maxHp: 240,
            attackDamage: 24,
            attackIntervalTicks: 22,
            movementTicksPerHex: 10,
            attackRangeHexes: 1,
            playerSelectable: false,
            damageType: DamageType.Blunt,
            slashingResist: 0, bluntResist: 0, piercingResist: 0,
            fireResist: 0, frostResist: 0, natureResist: 0, shadowResist: 0),
    ];
}
