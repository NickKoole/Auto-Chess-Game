using GameSim;

namespace Api.Battles;

/// <summary>
/// A named, hardcoded pair of armies: the id a client requests, and the
/// placements <see cref="GameSim.BattleSetup.TryCreate"/> builds a battle from.
///
/// Hardcoded rather than client-submitted: letting the client build a setup is
/// a later epic, and keeping it server-side means the client cannot submit an
/// illegal placement.
/// </summary>
/// <param name="Id">Stable identifier, unique within <see cref="MatchupCatalog"/>.</param>
/// <param name="PlayerPlacements">The player's army.</param>
/// <param name="OpponentPlacements">The opponent's army.</param>
public sealed record Matchup(
    string Id,
    IReadOnlyList<UnitPlacement> PlayerPlacements,
    IReadOnlyList<UnitPlacement> OpponentPlacements);
