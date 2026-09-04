using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Api.Battles.Dtos;
using GameSim;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Api.Battles;

public static class BattleEndpoints
{
    public static IEndpointRouteBuilder MapBattleEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/battles/matchups", Ok<IReadOnlyList<MatchupDto>> (MatchupCatalog matchups) =>
            TypedResults.Ok<IReadOnlyList<MatchupDto>>(
                [.. matchups.All.Select(m => new MatchupDto { Id = m.Id, DisplayName = m.DisplayName })]))
        .WithName("ListMatchups");

        app.MapGet("/battles/units", Ok<IReadOnlyList<UnitDefinitionDto>> (IUnitCatalog units) =>
            TypedResults.Ok<IReadOnlyList<UnitDefinitionDto>>(
                [.. units.All.Select(BattleResponseFactory.MapUnitDefinition)]))
        .WithName("ListUnits");

        app.MapGet("/battles/enemy-setups", Ok<IReadOnlyList<EnemySetupDto>> (EnemySetupCatalog enemySetups) =>
            TypedResults.Ok<IReadOnlyList<EnemySetupDto>>(
                [.. enemySetups.All.Select(s => new EnemySetupDto { Id = s.Id, DisplayName = s.DisplayName })]))
        .WithName("ListEnemySetups");

        // TypedResults, not Results: Results.Ok erases the response type, and
        // the OpenAPI generator needs it present to emit a real schema.
        app.MapPost("/battles/simulate", Results<Ok<SimulateBattleResponse>, BadRequest<ErrorResponse>> (
            SimulateBattleRequest request, MatchupCatalog matchups, EnemySetupCatalog enemySetups, IUnitCatalog units) =>
        {
            IReadOnlyList<UnitPlacement> playerPlacements;
            IReadOnlyList<UnitPlacement> opponentPlacements;

            // Only the player-submitted path can fail setup validation: a
            // hardcoded matchup or enemy setup is always legal by construction.
            bool playerSubmitted;

            if (!string.IsNullOrWhiteSpace(request.MatchupId))
            {
                if (!matchups.TryGet(request.MatchupId, out Matchup? matchup))
                {
                    return TypedResults.BadRequest(new ErrorResponse($"Unknown matchup id '{request.MatchupId}'."));
                }

                playerPlacements = matchup.PlayerPlacements;
                opponentPlacements = matchup.OpponentPlacements;
                playerSubmitted = false;
            }
            else if (!string.IsNullOrWhiteSpace(request.EnemySetupId))
            {
                if (!enemySetups.TryGet(request.EnemySetupId, out EnemySetup? enemySetup))
                {
                    return TypedResults.BadRequest(
                        new ErrorResponse($"Unknown enemy setup id '{request.EnemySetupId}'."));
                }

                if (request.PlayerPlacements is null)
                {
                    return TypedResults.BadRequest(new ErrorResponse("playerPlacements is required."));
                }

                if (!TryResolvePlacements(
                    units, request.PlayerPlacements, out IReadOnlyList<UnitPlacement>? resolved, out string? error))
                {
                    return TypedResults.BadRequest(new ErrorResponse(error));
                }

                playerPlacements = resolved;
                opponentPlacements = enemySetup.Placements;
                playerSubmitted = true;
            }
            else
            {
                return TypedResults.BadRequest(new ErrorResponse("Request must include either matchupId or enemySetupId."));
            }

            ulong seed;
            if (request.Seed is null)
            {
                seed = SeedGenerator.NewSeed();
            }
            else if (!ulong.TryParse(request.Seed, NumberStyles.None, CultureInfo.InvariantCulture, out seed))
            {
                return TypedResults.BadRequest(new ErrorResponse(
                    $"Seed '{request.Seed}' is not a valid unsigned 64-bit integer."));
            }

            if (!BattleSetup.TryCreate(
                playerPlacements, opponentPlacements, out BattleSetup? setup, out IReadOnlyList<PlacementError> setupErrors))
            {
                if (!playerSubmitted)
                {
                    // Unreachable: every hardcoded matchup and enemy setup is built from legal placements.
                    throw new InvalidOperationException(
                        "Hardcoded matchup/enemy setup failed setup validation. Both must always be legal.");
                }

                return TypedResults.BadRequest(
                    new ErrorResponse(string.Join(" ", setupErrors.Select(e => e.Message))));
            }

            var simulation = new BattleSimulation(setup, seed);
            BattleResult result = simulation.Run();

            return TypedResults.Ok(BattleResponseFactory.Create(seed, setup, simulation.Events, result));
        })
        .WithName("SimulateBattle");

        return app;
    }

    private static bool TryResolvePlacements(
        IUnitCatalog units,
        IReadOnlyList<PlayerPlacementDto> submitted,
        [NotNullWhen(true)] out IReadOnlyList<UnitPlacement>? placements,
        [NotNullWhen(false)] out string? error)
    {
        var resolved = new List<UnitPlacement>(submitted.Count);

        foreach (PlayerPlacementDto placement in submitted)
        {
            UnitDefinition? definition = units.All.FirstOrDefault(d => d.Id == placement.UnitId);
            if (definition is null)
            {
                placements = null;
                error = $"Unknown unit id '{placement.UnitId}'.";
                return false;
            }

            resolved.Add(new UnitPlacement(definition, new HexCoord(placement.Position.Row, placement.Position.Col)));
        }

        placements = resolved;
        error = null;
        return true;
    }
}
