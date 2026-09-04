using GameSim;

namespace Api.Battles;

/// <summary>
/// A named, hardcoded pair of armies: the id a client requests, and the
/// placements <see cref="GameSim.BattleSetup.TryCreate"/> builds a battle from.
/// Hardcoded rather than client-submitted, so a client can never submit an
/// illegal placement.
/// </summary>
/// <param name="Id">Stable identifier, unique within <see cref="MatchupCatalog"/>.</param>
/// <param name="DisplayName">What the picker shows a player, instead of the id.</param>
/// <param name="PlayerPlacements">The player's army.</param>
/// <param name="OpponentPlacements">The opponent's army.</param>
public sealed record Matchup(
    string Id,
    string DisplayName,
    IReadOnlyList<UnitPlacement> PlayerPlacements,
    IReadOnlyList<UnitPlacement> OpponentPlacements);
