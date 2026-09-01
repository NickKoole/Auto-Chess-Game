using System.Globalization;
using GameSim;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Api.Battles;

/// <summary>
/// Registers every battle-related route. Kept out of <c>Program.cs</c> so the
/// composition root stays a list of "what this app has" rather than growing a
/// route's worth of logic inline for every endpoint added to it.
/// </summary>
public static class BattleEndpoints
{
    public static IEndpointRouteBuilder MapBattleEndpoints(this IEndpointRouteBuilder app)
    {
        // TypedResults, not Results: the generic result type is what lets the
        // OpenAPI document carry a real response schema, which is what the
        // client generates its types from. Results.Ok erases it and yields an
        // untyped contract.
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
