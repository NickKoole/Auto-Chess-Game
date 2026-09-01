using System.Globalization;
using Api.Battles;
using Api.Persistence;
using GameSim;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.AddDbContext<GameDbContext>(options =>
    options.UseSqlite(
        builder.Configuration.GetConnectionString("GameDb")
        ?? "Data Source=autochess.db"));

builder.Services.AddSingleton<IUnitCatalog, StaticUnitCatalog>();
builder.Services.AddSingleton<MatchupCatalog>();

var app = builder.Build();

// The OpenAPI document is the contract the client's types are generated from,
// so it is served in every environment rather than development only.
app.MapOpenApi();

// TypedResults, not Results: the generic result type is what lets the OpenAPI
// document carry a real response schema, which is what the client generates
// its types from. Results.Ok erases it and yields an untyped contract.
app.MapGet("/health", () => TypedResults.Ok(new HealthResponse("ok")))
   .WithName("GetHealth");

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

    if (!BattleSetup.TryCreate(matchup.PlayerPlacements, matchup.OpponentPlacements, out BattleSetup? setup, out _))
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

app.Run();

/// <summary>Liveness payload.</summary>
public sealed record HealthResponse(string Status);

/// <summary>Exposed so <c>WebApplicationFactory</c> can boot the app in tests.</summary>
public partial class Program;
