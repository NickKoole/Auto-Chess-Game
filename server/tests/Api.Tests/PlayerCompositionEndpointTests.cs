using System.Net;
using System.Net.Http.Json;
using Api.Battles.Dtos;

namespace Api.Tests;

public sealed class PlayerCompositionEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Runs_a_battle_from_a_player_submitted_composition_against_an_enemy_setup()
    {
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync("/battles/simulate", new SimulateBattleRequest
        {
            EnemySetupId = "elite-line",
            PlayerPlacements =
            [
                new PlayerPlacementDto { UnitId = "knight", Position = new HexCoordDto { Row = 3, Col = 2 } },
                new PlayerPlacementDto { UnitId = "archer", Position = new HexCoordDto { Row = 4, Col = 1 } },
            ],
            Seed = "24680",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        SimulateBattleResponse? body = await response.Content.ReadFromJsonAsync<SimulateBattleResponse>();

        Assert.NotNull(body);
        Assert.Equal("24680", body.Seed);
        Assert.Equal(4, body.InitialSetup.Count);
        Assert.Contains(body.InitialSetup, u => u.DefinitionId == "knight" && u.Side == SideDto.Player);
        Assert.Contains(body.InitialSetup, u => u.DefinitionId == "archer" && u.Side == SideDto.Player);
        Assert.Equal(2, body.InitialSetup.Count(u => u.Side == SideDto.Opponent && u.DefinitionId == "knight"));
    }

    [Fact]
    public async Task Rejects_an_unknown_unit_id_naming_it()
    {
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync("/battles/simulate", new SimulateBattleRequest
        {
            EnemySetupId = "swarm",
            PlayerPlacements = [new PlayerPlacementDto { UnitId = "ghost", Position = new HexCoordDto { Row = 2, Col = 1 } }],
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        ErrorResponse? body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(body);
        Assert.Contains("ghost", body.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rejects_an_empty_placement_list_reusing_BattleSetups_no_units_message()
    {
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync("/battles/simulate", new SimulateBattleRequest
        {
            EnemySetupId = "swarm",
            PlayerPlacements = [],
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        ErrorResponse? body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(body);
        Assert.Contains("brought no units", body.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Rejects_a_placement_off_the_board()
    {
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync("/battles/simulate", new SimulateBattleRequest
        {
            EnemySetupId = "swarm",
            PlayerPlacements = [new PlayerPlacementDto { UnitId = "knight", Position = new HexCoordDto { Row = 2, Col = 99 } }],
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        ErrorResponse? body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(body);
        Assert.Contains("off the board", body.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Rejects_a_placement_in_the_neutral_band()
    {
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync("/battles/simulate", new SimulateBattleRequest
        {
            EnemySetupId = "swarm",
            PlayerPlacements = [new PlayerPlacementDto { UnitId = "knight", Position = new HexCoordDto { Row = 2, Col = 3 } }],
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        ErrorResponse? body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(body);
        Assert.Contains("neutral band", body.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Rejects_a_placement_in_the_opponents_deployment_band()
    {
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync("/battles/simulate", new SimulateBattleRequest
        {
            EnemySetupId = "swarm",
            PlayerPlacements = [new PlayerPlacementDto { UnitId = "knight", Position = new HexCoordDto { Row = 2, Col = 5 } }],
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        ErrorResponse? body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(body);
        Assert.Contains("opposing deployment band", body.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Rejects_two_placements_on_the_same_tile()
    {
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync("/battles/simulate", new SimulateBattleRequest
        {
            EnemySetupId = "swarm",
            PlayerPlacements =
            [
                new PlayerPlacementDto { UnitId = "knight", Position = new HexCoordDto { Row = 2, Col = 1 } },
                new PlayerPlacementDto { UnitId = "archer", Position = new HexCoordDto { Row = 2, Col = 1 } },
            ],
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        ErrorResponse? body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(body);
        Assert.Contains("more than once", body.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Rejects_an_unknown_enemy_setup_id_naming_it()
    {
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync("/battles/simulate", new SimulateBattleRequest
        {
            EnemySetupId = "does-not-exist",
            PlayerPlacements = [new PlayerPlacementDto { UnitId = "knight", Position = new HexCoordDto { Row = 2, Col = 1 } }],
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        ErrorResponse? body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(body);
        Assert.Contains("does-not-exist", body.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rejects_a_request_with_neither_matchupId_nor_enemySetupId()
    {
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/battles/simulate", new SimulateBattleRequest());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Existing_matchupId_requests_still_work_unchanged()
    {
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync("/battles/simulate", new SimulateBattleRequest
        {
            MatchupId = "melee-mirror",
            Seed = "12345",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
