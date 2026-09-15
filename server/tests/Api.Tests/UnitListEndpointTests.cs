using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Battles.Dtos;
using GameSim;

namespace Api.Tests;

public sealed class UnitListEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Lists_every_unit_in_the_catalog()
    {
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/battles/units");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        List<UnitDefinitionDto>? body = await response.Content.ReadFromJsonAsync<List<UnitDefinitionDto>>();

        Assert.NotNull(body);

        var expectedIds = new StaticUnitCatalog().All.Where(d => d.PlayerSelectable).Select(d => d.Id).ToHashSet();
        var actualIds = body.Select(d => d.Id).ToHashSet();
        Assert.Equal(expectedIds, actualIds);
    }

    [Fact]
    public async Task Reports_no_duplicate_ids()
    {
        HttpClient client = factory.CreateClient();

        List<UnitDefinitionDto>? body = await client.GetFromJsonAsync<List<UnitDefinitionDto>>("/battles/units");

        Assert.NotNull(body);
        Assert.Equal(body.Count, body.Select(d => d.Id).Distinct().Count());
    }

    [Fact]
    public async Task Reports_the_same_stats_the_catalog_holds()
    {
        HttpClient client = factory.CreateClient();

        List<UnitDefinitionDto>? body = await client.GetFromJsonAsync<List<UnitDefinitionDto>>("/battles/units");
        Assert.NotNull(body);

        UnitDefinition knight = new StaticUnitCatalog().Get("knight");
        UnitDefinitionDto? dto = body.SingleOrDefault(d => d.Id == "knight");

        Assert.NotNull(dto);
        Assert.Equal(knight.DisplayName, dto.DisplayName);
        Assert.Equal(knight.MaxHp, dto.MaxHp);
        Assert.Equal(knight.AttackDamage, dto.AttackDamage);
        Assert.Equal(knight.AttackIntervalTicks, dto.AttackIntervalTicks);
        Assert.Equal(knight.MovementTicksPerHex, dto.MovementTicksPerHex);
        Assert.Equal(knight.AttackRangeHexes, dto.AttackRangeHexes);
        Assert.Equal(knight.DamageVariance, dto.DamageVariance);
    }

    [Fact]
    public async Task Response_JSON_uses_camelCase_property_names()
    {
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/battles/units");
        string json = await response.Content.ReadAsStringAsync();
        using JsonDocument document = JsonDocument.Parse(json);

        JsonElement first = document.RootElement[0];
        Assert.True(first.TryGetProperty("id", out _));
        Assert.True(first.TryGetProperty("displayName", out _));
        Assert.True(first.TryGetProperty("maxHp", out _));
    }
}
