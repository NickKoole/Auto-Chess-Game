using System.Net;
using System.Net.Http.Json;
using Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

public sealed class SmokeTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Health_endpoint_responds()
    {
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.Equal("ok", body?.Status);
    }

    [Fact]
    public async Task OpenApi_document_is_served()
    {
        // The client's TypeScript types are generated from this document, so a
        // broken or unserved contract must fail here rather than in the client.
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string json = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"/health\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Migrations_apply_and_the_schema_round_trips()
    {
        using IServiceScope scope = factory.Services.CreateScope();
        GameDbContext db = scope.ServiceProvider.GetRequiredService<GameDbContext>();

        db.PlayerSaves.Add(new PlayerSave
        {
            Id = Guid.NewGuid(),
            PlayerName = "nick",
            CreatedAtUtc = DateTimeOffset.UnixEpoch,
            LastAdvancedAtUtc = DateTimeOffset.UnixEpoch,
            LastTickProcessed = 0,
            StateJson = "{}",
        });
        await db.SaveChangesAsync();

        PlayerSave? loaded = await db.PlayerSaves.SingleOrDefaultAsync(p => p.PlayerName == "nick");

        Assert.NotNull(loaded);
        Assert.Equal(0, loaded.LastTickProcessed);
    }
}
