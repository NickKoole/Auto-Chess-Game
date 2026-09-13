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
        Assert.Equal(4, body.Count);
        Assert.Contains(body, s => s.Id == "elite-line");
        Assert.Contains(body, s => s.Id == "mixed-line");
        Assert.Contains(body, s => s.Id == "swarm");
        Assert.Contains(body, s => s.Id == "guarded-archers");
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
        Assert.True(first.TryGetProperty("placements", out JsonElement placements));

        JsonElement firstPlacement = placements[0];
        Assert.True(firstPlacement.TryGetProperty("definitionId", out _));
        Assert.True(firstPlacement.TryGetProperty("position", out JsonElement position));
        Assert.True(position.TryGetProperty("row", out _));
        Assert.True(position.TryGetProperty("col", out _));
    }

    [Fact]
    public async Task Exposes_each_setups_placements_matching_the_catalog()
    {
        HttpClient client = factory.CreateClient();
        var catalog = new EnemySetupCatalog(new StaticUnitCatalog());

        List<EnemySetupDto>? body = await client.GetFromJsonAsync<List<EnemySetupDto>>("/battles/enemy-setups");

        Assert.NotNull(body);
        foreach (EnemySetupDto dto in body)
        {
            EnemySetup expected = catalog.All.Single(s => s.Id == dto.Id);

            var expectedPlacements = expected.Placements
                .Select(p => (p.Definition.Id, p.Coord.Row, p.Coord.Col))
                .OrderBy(t => t.Id).ThenBy(t => t.Row).ThenBy(t => t.Col)
                .ToList();
            var actualPlacements = dto.Placements
                .Select(p => (p.DefinitionId, p.Position.Row, p.Position.Col))
                .OrderBy(t => t.DefinitionId).ThenBy(t => t.Row).ThenBy(t => t.Col)
                .ToList();

            Assert.Equal(expectedPlacements, actualPlacements);
        }
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

    [Fact]
    public void Guarded_archers_places_two_knights_at_the_front_column_and_two_archers_behind_them()
    {
        var catalog = new EnemySetupCatalog(new StaticUnitCatalog());
        EnemySetup setup = catalog.All.Single(s => s.Id == "guarded-archers");

        List<HexCoord> knightCoords = [.. setup.Placements.Where(p => p.Definition.Id == "knight").Select(p => p.Coord)];
        List<HexCoord> archerCoords = [.. setup.Placements.Where(p => p.Definition.Id == "archer").Select(p => p.Coord)];

        Assert.Equal(2, knightCoords.Count);
        Assert.Equal(2, archerCoords.Count);
        Assert.Equal(setup.Placements.Count, knightCoords.Count + archerCoords.Count);

        Assert.All(knightCoords, c => Assert.Equal(5, c.Col));
        Assert.All(archerCoords, c => Assert.Equal(7, c.Col));
    }
}
