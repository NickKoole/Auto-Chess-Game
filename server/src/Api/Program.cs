using Api.Battles;
using Api.Persistence;
using GameSim;
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

// Served in every environment, not just development: it's the contract the
// client's types are generated from.
app.MapOpenApi();

// TypedResults, not Results: Results.Ok erases the response type, and the
// OpenAPI generator needs it present to emit a real schema.
app.MapGet("/health", () => TypedResults.Ok(new HealthResponse("ok")))
   .WithName("GetHealth");

app.MapBattleEndpoints();

app.Run();

/// <summary>Liveness payload.</summary>
public sealed record HealthResponse(string Status);

/// <summary>Exposed so <c>WebApplicationFactory</c> can boot the app in tests.</summary>
public partial class Program;
