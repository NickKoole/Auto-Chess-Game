using System.Net;
using System.Net.Http.Json;
using Api.Battles;
using Api.Battles.Dtos;
using GameSim;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Api.Tests;

/// <summary>
/// Confirms <c>PlayerSelectable</c> gates the roster endpoint only, exactly as
/// designed: the catalog and everything that resolves ids against it -
/// <see cref="EnemySetupCatalog"/> included - stays unfiltered.
/// </summary>
public sealed class PlayerSelectableRosterEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string HiddenUnitId = "shadow-wolf";

    private static IUnitCatalog BuildCatalogWithHiddenUnit() => new StaticUnitCatalog(
    [
        .. new StaticUnitCatalog().All,
        new UnitDefinition(
            id: HiddenUnitId,
            displayName: "Shadow Wolf",
            maxHp: 80,
            attackDamage: 18,
            attackIntervalTicks: 10,
            movementTicksPerHex: 5,
            attackRangeHexes: 1,
            playerSelectable: false),
    ]);

    private HttpClient CreateClientWithHiddenUnit()
    {
        WebApplicationFactory<Program> customized = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IUnitCatalog>();
                services.AddSingleton(BuildCatalogWithHiddenUnit());
            }));

        return customized.CreateClient();
    }

    [Fact]
    public async Task Roster_endpoint_excludes_a_non_selectable_unit()
    {
        HttpClient client = CreateClientWithHiddenUnit();

        List<UnitDefinitionDto>? body = await client.GetFromJsonAsync<List<UnitDefinitionDto>>("/battles/units");

        Assert.NotNull(body);
        Assert.DoesNotContain(body, d => d.Id == HiddenUnitId);

        var expectedSelectableIds = new StaticUnitCatalog().All.Select(d => d.Id).ToHashSet();
        Assert.Equal(expectedSelectableIds, body.Select(d => d.Id).ToHashSet());
    }

    [Fact]
    public async Task Simulate_rejects_a_player_placement_referencing_a_non_selectable_unit()
    {
        HttpClient client = CreateClientWithHiddenUnit();

        HttpResponseMessage response = await client.PostAsJsonAsync("/battles/simulate", new SimulateBattleRequest
        {
            EnemySetupId = "swarm",
            PlayerPlacements =
            [
                new PlayerPlacementDto { UnitId = HiddenUnitId, Position = new HexCoordDto { Row = 2, Col = 2 } },
            ],
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        ErrorResponse? body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(body);
        Assert.Contains(HiddenUnitId, body.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_non_selectable_unit_still_simulates_as_an_enemy_setup_placement()
    {
        IUnitCatalog catalog = BuildCatalogWithHiddenUnit();

        // Exactly the shape EnemySetupCatalog.BuiltIn produces: a hand-authored
        // EnemySetup resolving its placements from the shared, unfiltered catalog.
        var enemySetup = new EnemySetup(
            "hidden-wolf",
            DisplayName: "Hidden Wolf",
            Placements: [new UnitPlacement(catalog.Get(HiddenUnitId), new HexCoord(2, 7))]);

        IReadOnlyList<UnitPlacement> playerPlacements =
        [
            new UnitPlacement(catalog.Get("knight"), new HexCoord(2, 4)),
        ];

        Assert.True(BattleSetup.TryCreate(
            playerPlacements, enemySetup.Placements, out BattleSetup? setup, out IReadOnlyList<PlacementError> errors));
        Assert.Empty(errors);
        Assert.NotNull(setup);

        var simulation = new BattleSimulation(setup, seed: 1);
        BattleResult result = simulation.Run();

        Assert.True(Enum.IsDefined(result.Outcome));
        Assert.Contains(
            simulation.Events.OfType<UnitSpawnedEvent>(),
            e => e.DefinitionId == HiddenUnitId && e.Side == BattleSide.Opponent);
    }
}
