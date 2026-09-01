namespace Api.Battles.Dtos;

/// <summary>How a finished battle came to: who won, when, and exactly how.</summary>
public sealed record BattleOutcomeDto
{
    public required MatchWinnerDto Winner { get; init; }

    /// <summary>The tick the battle ended on. See <see cref="GameSim.BattleResult.EndTick"/>.</summary>
    public required int TerminatingTick { get; init; }

    public required BattleEndingDto TerminationReason { get; init; }
}
