using System.Diagnostics.CodeAnalysis;
using GameSim;

namespace Api.Battles;

/// <summary>
/// The small set of hand-authored enemy armies a player-built composition
/// can be run against. Each deploys in the opponent's rows 0-2 of the 8x8
/// board - <see cref="Battlefield.DeploymentBand"/>.
/// </summary>
public sealed class EnemySetupCatalog
{
    private readonly Dictionary<string, EnemySetup> _byId;

    public EnemySetupCatalog(IUnitCatalog units)
    {
        ArgumentNullException.ThrowIfNull(units);

        _byId = new Dictionary<string, EnemySetup>(StringComparer.Ordinal);

        // Source order, captured once, mirroring MatchupCatalog: the
        // dictionary above is only ever used for lookup, never enumeration.
        var all = new List<EnemySetup>();

        foreach (EnemySetup setup in BuiltIn(units))
        {
            _byId.Add(setup.Id, setup);
            all.Add(setup);
        }

        All = all;
    }

    public bool TryGet(string id, [NotNullWhen(true)] out EnemySetup? setup) =>
        _byId.TryGetValue(id, out setup);

    /// <summary>
    /// Every setup this catalog knows, in the order <see cref="BuiltIn"/>
    /// declares them - the same deterministic order a client's picker renders
    /// them in.
    /// </summary>
    public IReadOnlyList<EnemySetup> All { get; }

    private static IEnumerable<EnemySetup> BuiltIn(IUnitCatalog units)
    {
        yield return new EnemySetup(
            "elite-line",
            DisplayName: "Elite Line",
            Placements: FrontLine(units.Get("knight"), row: 2, cols: [3, 4]));

        yield return new EnemySetup(
            "mixed-line",
            DisplayName: "Mixed Line",
            Placements: Row(row: 2, cols: [2, 3, 4], units.Get("militia"), units.Get("knight"), units.Get("archer")));

        yield return new EnemySetup(
            "swarm",
            DisplayName: "Swarm",
            Placements: FrontLine(units.Get("militia"), row: 2, cols: [1, 2, 3, 4, 5, 6]));

        yield return new EnemySetup(
            "guarded-archers",
            DisplayName: "Guarded Archers",
            Placements:
            [
                .. FrontLine(units.Get("knight"), row: 2, cols: [3, 4]),
                .. FrontLine(units.Get("archer"), row: 0, cols: [3, 4]),
            ]);
    }

    private static IReadOnlyList<UnitPlacement> FrontLine(UnitDefinition definition, int row, int[] cols) =>
        [.. cols.Select(col => new UnitPlacement(definition, new HexCoord(row, col)))];

    /// <summary>One definition per column, paired in order - unlike <see cref="FrontLine"/>, a mixed row rather than one unit repeated.</summary>
    private static IReadOnlyList<UnitPlacement> Row(int row, int[] cols, params UnitDefinition[] definitions)
    {
        if (cols.Length != definitions.Length)
        {
            throw new ArgumentException(
                $"Row for row {row}: {cols.Length} columns but {definitions.Length} definitions - Zip would silently drop one.",
                nameof(definitions));
        }

        return [.. cols.Zip(definitions, (col, definition) => new UnitPlacement(definition, new HexCoord(row, col)))];
    }
}
