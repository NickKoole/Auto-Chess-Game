using System.Diagnostics.CodeAnalysis;
using GameSim;

namespace Api.Battles;

/// <summary>
/// The small set of hand-authored enemy armies a player-built composition
/// can be run against. Each deploys in the opponent's columns 7-11 of the
/// 12-column board - <see cref="Battlefield.DeploymentBand"/>.
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
            Placements: FrontLine(units.Get("knight"), col: 7, rows: [3, 4]));

        yield return new EnemySetup(
            "mixed-line",
            DisplayName: "Mixed Line",
            Placements: Column(col: 7, rows: [2, 3, 4], units.Get("militia"), units.Get("knight"), units.Get("archer")));

        yield return new EnemySetup(
            "swarm",
            DisplayName: "Swarm",
            Placements: FrontLine(units.Get("militia"), col: 7, rows: [1, 2, 3, 4, 5, 6]));

        yield return new EnemySetup(
            "guarded-archers",
            DisplayName: "Guarded Archers",
            Placements:
            [
                .. FrontLine(units.Get("knight"), col: 7, rows: [3, 4]),
                .. FrontLine(units.Get("archer"), col: 9, rows: [3, 4]),
            ]);
    }

    private static IReadOnlyList<UnitPlacement> FrontLine(UnitDefinition definition, int col, int[] rows) =>
        [.. rows.Select(row => new UnitPlacement(definition, new HexCoord(row, col)))];

    /// <summary>One definition per row, paired in order - unlike <see cref="FrontLine"/>, a mixed column rather than one unit repeated.</summary>
    private static IReadOnlyList<UnitPlacement> Column(int col, int[] rows, params UnitDefinition[] definitions)
    {
        if (rows.Length != definitions.Length)
        {
            throw new ArgumentException(
                $"Column for column {col}: {rows.Length} rows but {definitions.Length} definitions - Zip would silently drop one.",
                nameof(definitions));
        }

        return [.. rows.Zip(definitions, (row, definition) => new UnitPlacement(definition, new HexCoord(row, col)))];
    }
}
