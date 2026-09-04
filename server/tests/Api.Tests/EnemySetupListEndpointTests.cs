using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Battles;
using Api.Battles.Dtos;
using GameSim;

namespace Api.Tests;

public sealed class EnemySetupListEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Lists_every_hardcoded_enemy_setup_with_an_id_and_a_display_name()
    {
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/battles/enemy-setups");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        List<EnemySetupDto>? body = await response.Content.ReadFromJsonAsync<List<EnemySetupDto>>();

        Assert.NotNull(body);
        Assert.Equal(3, body.Count);
        Assert.Contains(body, s => s.Id == "elite-line");
        Assert.Contains(body, s => s.Id == "mixed-line");
        Assert.Contains(body, s => s.Id == "swarm");
        Assert.All(body, s => Assert.False(string.IsNullOrWhiteSpace(s.DisplayName)));
    }

    [Fact]
    public async Task Reports_no_duplicate_ids()
    {
        HttpClient client = factory.CreateClient();

        List<EnemySetupDto>? body = await client.GetFromJsonAsync<List<EnemySetupDto>>("/battles/enemy-setups");

        Assert.NotNull(body);
        Assert.Equal(body.Count, body.Select(s => s.Id).Distinct().Count());
    }

    [Fact]
    public async Task Response_JSON_uses_camelCase_property_names()
    {
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/battles/enemy-setups");
        string json = await response.Content.ReadAsStringAsync();
        using JsonDocument document = JsonDocument.Parse(json);

        JsonElement first = document.RootElement[0];
        Assert.True(first.TryGetProperty("id", out _));
        Assert.True(first.TryGetProperty("displayName", out _));
    }

    [Fact]
    public void Every_setups_placements_are_legal_for_the_opponent_side_with_no_overlapping_tiles()
    {
        var catalog = new EnemySetupCatalog(new StaticUnitCatalog());

        foreach (EnemySetup setup in catalog.All)
        {
            Assert.NotEmpty(setup.Placements);

            foreach (UnitPlacement placement in setup.Placements)
            {
                PlacementError? error = Battlefield.CheckTile(BattleSide.Opponent, placement.Coord);
                Assert.Null(error);
            }

            var coords = setup.Placements.Select(p => p.Coord).ToList();
            Assert.Equal(coords.Count, coords.Distinct().Count());
        }
    }
}
