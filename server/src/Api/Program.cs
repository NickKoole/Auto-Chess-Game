using Api.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.AddDbContext<GameDbContext>(options =>
    options.UseSqlite(
        builder.Configuration.GetConnectionString("GameDb")
        ?? "Data Source=idleautochess.db"));

var app = builder.Build();

// The OpenAPI document is the contract the client's types are generated from,
// so it is served in every environment rather than development only.
app.MapOpenApi();

// TypedResults, not Results: the generic result type is what lets the OpenAPI
// document carry a real response schema, which is what the client generates
// its types from. Results.Ok erases it and yields an untyped contract.
app.MapGet("/health", () => TypedResults.Ok(new HealthResponse("ok")))
   .WithName("GetHealth");

app.Run();

/// <summary>Liveness payload.</summary>
public sealed record HealthResponse(string Status);

/// <summary>Exposed so <c>WebApplicationFactory</c> can boot the app in tests.</summary>
public partial class Program;
