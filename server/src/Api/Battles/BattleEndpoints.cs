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
            SimulateBattleRequest request, MatchupCatalog matchups) =>
        {
            if (!matchups.TryGet(request.MatchupId, out Matchup? matchup))
            {
                return TypedResults.BadRequest(new ErrorResponse($"Unknown matchup id '{request.MatchupId}'."));
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
                matchup.PlayerPlacements, matchup.OpponentPlacements, out BattleSetup? setup, out _))
            {
                // Unreachable: every hardcoded matchup is built from legal placements.
                throw new InvalidOperationException(
                    $"Matchup '{matchup.Id}' failed setup validation. Hardcoded matchups must always be legal.");
            }

            var simulation = new BattleSimulation(setup, seed);
            BattleResult result = simulation.Run();

            return TypedResults.Ok(BattleResponseFactory.Create(seed, setup, simulation.Events, result));
        })
        .WithName("SimulateBattle");

        return app;
    }
}
