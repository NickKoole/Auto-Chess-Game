namespace Api.Battles.Dtos;

public sealed record BattleOutcomeDto
{
    public required MatchWinnerDto Winner { get; init; }

    public required int TerminatingTick { get; init; }

    public required BattleEndingDto TerminationReason { get; init; }
}
