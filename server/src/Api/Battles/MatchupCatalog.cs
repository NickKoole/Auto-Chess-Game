using System.Diagnostics.CodeAnalysis;
using GameSim;

namespace Api.Battles;

/// <summary>
/// The small set of named matchups a battle can be requested for. Each
/// deploys the player in rows 5-7 and the opponent in rows 0-2 of the 8x8
/// board - <see cref="Battlefield.DeploymentBand"/> for each side.
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
            PlayerPlacements: FrontLine(units.Get("knight"), row: 5, cols: [2, 3, 4]),
            OpponentPlacements: FrontLine(units.Get("knight"), row: 2, cols: [2, 3, 4]));

        yield return new Matchup(
            "archers-vs-melee",
            DisplayName: "Archers vs Melee",
            PlayerPlacements: FrontLine(units.Get("archer"), row: 5, cols: [2, 3, 4]),
            OpponentPlacements: FrontLine(units.Get("militia"), row: 2, cols: [2, 3, 4]));
    }

    private static IReadOnlyList<UnitPlacement> FrontLine(UnitDefinition definition, int row, int[] cols) =>
        [.. cols.Select(col => new UnitPlacement(definition, new HexCoord(row, col)))];
}
