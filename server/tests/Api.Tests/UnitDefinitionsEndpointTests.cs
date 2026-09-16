using System.Net;
using System.Net.Http.Json;
using Api.Battles.Dtos;
using GameSim;

namespace Api.Tests;

public sealed class UnitDefinitionsEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Lists_every_unit_in_the_catalog_including_non_player_selectable_ones()
    {
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/battles/unit-definitions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        List<UnitDefinitionDto>? body = await response.Content.ReadFromJsonAsync<List<UnitDefinitionDto>>();

        Assert.NotNull(body);

        var expectedIds = new StaticUnitCatalog().All.Select(d => d.Id).ToHashSet();
        var actualIds = body.Select(d => d.Id).ToHashSet();
        Assert.Equal(expectedIds, actualIds);
    }

    [Fact]
    public async Task Includes_an_enemy_only_unit_with_its_real_max_hp()
    {
        HttpClient client = factory.CreateClient();

        List<UnitDefinitionDto>? body = await client.GetFromJsonAsync<List<UnitDefinitionDto>>("/battles/unit-definitions");
        Assert.NotNull(body);

        UnitDefinition goblin = new StaticUnitCatalog().Get("goblin");
        Assert.False(goblin.PlayerSelectable);

        UnitDefinitionDto? dto = body.SingleOrDefault(d => d.Id == "goblin");
        Assert.NotNull(dto);
        Assert.Equal(goblin.MaxHp, dto.MaxHp);
        Assert.NotEqual(0, dto.MaxHp);
    }

    [Fact]
    public async Task Reports_no_duplicate_ids()
    {
        HttpClient client = factory.CreateClient();

        List<UnitDefinitionDto>? body = await client.GetFromJsonAsync<List<UnitDefinitionDto>>("/battles/unit-definitions");

        Assert.NotNull(body);
        Assert.Equal(body.Count, body.Select(d => d.Id).Distinct().Count());
    }
}
