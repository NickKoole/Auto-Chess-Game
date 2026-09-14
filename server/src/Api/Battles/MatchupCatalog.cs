using System.Diagnostics.CodeAnalysis;
using GameSim;

namespace Api.Battles;

/// <summary>
/// The small set of named matchups a battle can be requested for. Each
/// deploys the player in columns 0-4 and the opponent in columns 7-11 of the
/// 12-column board - <see cref="Battlefield.DeploymentBand"/> for each side.
/// </summary>
public sealed class MatchupCatalog
{
    private readonly Dictionary<string, Matchup> _byId;

    public MatchupCatalog(IUnitCatalog units)
    {
        ArgumentNullException.ThrowIfNull(units);

        _byId = new Dictionary<string, Matchup>(StringComparer.Ordinal);

        // Source order, captured once, mirroring StaticUnitCatalog.All: the
        // dictionary above is only ever used for lookup, never enumeration.
        var all = new List<Matchup>();

        foreach (Matchup matchup in BuiltIn(units))
        {
            _byId.Add(matchup.Id, matchup);
            all.Add(matchup);
        }

        All = all;
    }

    public bool TryGet(string id, [NotNullWhen(true)] out Matchup? matchup) =>
        _byId.TryGetValue(id, out matchup);

    /// <summary>
    /// Every matchup this catalog knows, in the order <see cref="BuiltIn"/>
    /// declares them - the same deterministic order a client's picker renders
    /// them in, and never a <see cref="Dictionary{TKey, TValue}"/>'s enumeration
    /// order, which .NET does not guarantee.
    /// </summary>
    public IReadOnlyList<Matchup> All { get; }

    private static IEnumerable<Matchup> BuiltIn(IUnitCatalog units)
    {
        yield return new Matchup(
            "melee-mirror",
            DisplayName: "Melee Mirror",
            PlayerPlacements: FrontLine(units.Get("knight"), col: 2, rows: [2, 3, 4]),
            OpponentPlacements: FrontLine(units.Get("knight"), col: 7, rows: [2, 3, 4]));

        yield return new Matchup(
            "archers-vs-melee",
            DisplayName: "Archers vs Melee",
            PlayerPlacements: FrontLine(units.Get("archer"), col: 2, rows: [2, 3, 4]),
            OpponentPlacements: FrontLine(units.Get("militia"), col: 7, rows: [2, 3, 4]));

        yield return new Matchup(
            "knights-and-archers",
            DisplayName: "Knights and Archers",
            PlayerPlacements:
            [
                .. FrontLine(units.Get("knight"), col: 2, rows: [2, 3, 4]),
                .. FrontLine(units.Get("archer"), col: 1, rows: [2, 3, 4]),
            ],
            OpponentPlacements:
            [
                .. FrontLine(units.Get("knight"), col: 7, rows: [2, 3, 4]),
                .. FrontLine(units.Get("archer"), col: 8, rows: [2, 3, 4]),
            ]);

        yield return new Matchup(
            "all-varieties",
            DisplayName: "All Varieties",
            PlayerPlacements:
            [
                .. Column(col: 2, rows: [2, 3, 4], units.Get("militia"), units.Get("knight"), units.Get("berserker")),
                .. Column(col: 1, rows: [2, 3, 4], units.Get("archer"), units.Get("crossbowman"), units.Get("mage")),
            ],
            OpponentPlacements:
            [
                .. Column(col: 7, rows: [2, 3, 4], units.Get("militia"), units.Get("knight"), units.Get("berserker")),
                .. Column(col: 8, rows: [2, 3, 4], units.Get("archer"), units.Get("crossbowman"), units.Get("mage")),
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
