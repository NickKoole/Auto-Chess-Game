using GameSim;

namespace Api.Battles;

/// <summary>
/// A hardcoded enemy army a player-built composition can be run against.
/// Opponent-side only - unlike <see cref="Matchup"/>, there is no player
/// side to pair it with. <see cref="Id"/> is unique within <see cref="EnemySetupCatalog"/>.
/// </summary>
public sealed record EnemySetup(string Id, string DisplayName, IReadOnlyList<UnitPlacement> Placements);
