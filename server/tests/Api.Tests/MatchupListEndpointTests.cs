using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Battles.Dtos;

namespace Api.Tests;

public sealed class MatchupListEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Lists_every_hardcoded_matchup_with_an_id_and_a_display_name()
    {
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/battles/matchups");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        List<MatchupDto>? body = await response.Content.ReadFromJsonAsync<List<MatchupDto>>();

        Assert.NotNull(body);
        Assert.NotEmpty(body);
        Assert.Contains(body, m => m.Id == "melee-mirror");
        Assert.Contains(body, m => m.Id == "archers-vs-melee");

        // The whole point of this endpoint: a display name for every id, so the
        // client never has to fall back to showing the raw identifier.
        Assert.All(body, m => Assert.False(string.IsNullOrWhiteSpace(m.DisplayName)));

        // Every SimulateBattleRequest.MatchupId this endpoint advertises must
        // actually be requestable - a listed id the simulate endpoint rejects
        // would be a broken picker entry.
        foreach (MatchupDto matchup in body)
        {
            HttpResponseMessage simulate = await client.PostAsJsonAsync("/battles/simulate", new SimulateBattleRequest
            {
                MatchupId = matchup.Id,
            });
            Assert.Equal(HttpStatusCode.OK, simulate.StatusCode);
        }
    }

    [Fact]
    public async Task Reports_no_duplicate_ids()
    {
        HttpClient client = factory.CreateClient();

        List<MatchupDto>? body = await client.GetFromJsonAsync<List<MatchupDto>>("/battles/matchups");

        Assert.NotNull(body);
        Assert.Equal(body.Count, body.Select(m => m.Id).Distinct().Count());
    }

    [Fact]
    public async Task Response_JSON_uses_camelCase_property_names()
    {
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/battles/matchups");
        string json = await response.Content.ReadAsStringAsync();
        using JsonDocument document = JsonDocument.Parse(json);

        JsonElement first = document.RootElement[0];
        Assert.True(first.TryGetProperty("id", out _));
        Assert.True(first.TryGetProperty("displayName", out _));
    }
}
