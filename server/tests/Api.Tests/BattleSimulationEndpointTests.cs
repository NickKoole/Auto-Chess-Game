using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Battles.Dtos;

namespace Api.Tests;

public sealed class BattleSimulationEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Simulating_a_known_matchup_with_a_fixed_seed_returns_a_full_replay()
    {
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync("/battles/simulate", new SimulateBattleRequest
        {
            MatchupId = "melee-mirror",
            Seed = "12345",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        SimulateBattleResponse? body = await response.Content.ReadFromJsonAsync<SimulateBattleResponse>();

        Assert.NotNull(body);
        Assert.Equal("12345", body.Seed);
        Assert.NotEmpty(body.InitialSetup);
        Assert.NotEmpty(body.UnitDefinitions);
        Assert.NotEmpty(body.Events);
        Assert.NotNull(body.Outcome);
        Assert.True(Enum.IsDefined(body.Outcome.Winner));
        Assert.True(Enum.IsDefined(body.Outcome.TerminationReason));

        // Every definition id the log and setup refer to must actually be present,
        // so a client never has to fall back to a second call or its own catalog.
        var knownDefinitionIds = body.UnitDefinitions.Select(d => d.Id).ToHashSet();
        Assert.All(body.InitialSetup, unit => Assert.Contains(unit.DefinitionId, knownDefinitionIds));
    }

    [Fact]
    public async Task Simulating_the_same_matchup_and_seed_twice_produces_identical_responses()
    {
        HttpClient client = factory.CreateClient();
        var request = new SimulateBattleRequest { MatchupId = "archers-vs-melee", Seed = "987654321" };

        HttpResponseMessage first = await client.PostAsJsonAsync("/battles/simulate", request);
        HttpResponseMessage second = await client.PostAsJsonAsync("/battles/simulate", request);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        string firstBody = await first.Content.ReadAsStringAsync();
        string secondBody = await second.Content.ReadAsStringAsync();

        Assert.Equal(firstBody, secondBody);
    }

    [Fact]
    public async Task Omitting_the_seed_generates_one_and_reports_it()
    {
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync("/battles/simulate", new SimulateBattleRequest
        {
            MatchupId = "melee-mirror",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        SimulateBattleResponse? body = await response.Content.ReadFromJsonAsync<SimulateBattleResponse>();

        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body.Seed));
        Assert.True(ulong.TryParse(body.Seed, out _));
    }

    [Fact]
    public async Task Unknown_matchup_id_returns_400_naming_the_identifier()
    {
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync("/battles/simulate", new SimulateBattleRequest
        {
            MatchupId = "does-not-exist",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        ErrorResponse? body = await response.Content.ReadFromJsonAsync<ErrorResponse>();

        Assert.NotNull(body);
        Assert.Contains("does-not-exist", body.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Malformed_seed_returns_400()
    {
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync("/battles/simulate", new SimulateBattleRequest
        {
            MatchupId = "melee-mirror",
            Seed = "not-a-number",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Response_JSON_uses_camelCase_property_names()
    {
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync("/battles/simulate", new SimulateBattleRequest
        {
            MatchupId = "melee-mirror",
            Seed = "1",
        });

        string json = await response.Content.ReadAsStringAsync();
        using JsonDocument document = JsonDocument.Parse(json);

        JsonElement root = document.RootElement;
        Assert.True(root.TryGetProperty("initialSetup", out _));
        Assert.True(root.TryGetProperty("unitDefinitions", out _));

        JsonElement firstUnit = root.GetProperty("initialSetup")[0];
        Assert.True(firstUnit.TryGetProperty("unitId", out _));
        Assert.True(firstUnit.TryGetProperty("definitionId", out _));

        // int properties still serialize as plain JSON numbers - the OpenAPI
        // schema's broader "integer or string" type is a document-generation
        // detail, not the actual wire shape.
        Assert.Equal(JsonValueKind.Number, firstUnit.GetProperty("unitId").ValueKind);
    }
}
