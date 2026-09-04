using GameSim;

namespace Api.Battles;

/// <summary>
/// Hardcoded rather than client-submitted, so a client can never submit an
/// illegal placement. <see cref="Id"/> is unique within <see cref="MatchupCatalog"/>.
/// </summary>
public sealed record Matchup(
    string Id,
    string DisplayName,
    IReadOnlyList<UnitPlacement> PlayerPlacements,
    IReadOnlyList<UnitPlacement> OpponentPlacements);
