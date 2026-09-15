using GameSim;

namespace GameSim.Tests;

public sealed class StaticUnitCatalogTests
{
    private static readonly StaticUnitCatalog Catalog = new();

    private static UnitDefinition Unit(string id, int range = 1, bool playerSelectable = true) =>
        new(id, id, maxHp: 100, attackDamage: 10, attackIntervalTicks: 10,
            movementTicksPerHex: 5, attackRangeHexes: range, playerSelectable: playerSelectable);

    [Fact]
    public void Looks_a_definition_up_by_id()
    {
        UnitDefinition archer = Catalog.Get("archer");

        Assert.Equal("archer", archer.Id);
        Assert.Equal("Archer", archer.DisplayName);
    }

    [Fact]
    public void Returns_the_same_shared_instance_every_time()
    {
        // The definition is a template, not a copy handed out per call.
        Assert.Same(Catalog.Get("knight"), Catalog.Get("knight"));
    }

    [Fact]
    public void Every_listed_definition_can_be_looked_up_by_its_own_id()
    {
        foreach (UnitDefinition definition in Catalog.All)
        {
            Assert.Same(definition, Catalog.Get(definition.Id));
        }
    }

    [Fact]
    public void Unknown_id_throws_an_error_naming_the_id()
    {
        var ex = Assert.Throws<KeyNotFoundException>(() => Catalog.Get("griffon"));

        Assert.Contains("griffon", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Missing_id_is_rejected_rather_than_reported_as_unknown(string? id)
    {
        // Not KeyNotFoundException: a blank id is a caller bug, distinct from
        // asking for a unit that genuinely is not in the roster.
        Assert.ThrowsAny<ArgumentException>(() => Catalog.Get(id!));
    }

    [Fact]
    public void Lookup_is_case_sensitive()
    {
        // Ids are code identifiers, not user input. Matching loosely would let
        // two spellings of the same unit drift apart unnoticed.
        Assert.Throws<KeyNotFoundException>(() => Catalog.Get("Archer"));
    }

    [Fact]
    public void Holds_at_least_six_definitions()
    {
        Assert.True(
            Catalog.All.Count >= 6,
            $"Expected at least 6 definitions, found {Catalog.All.Count}.");
    }

    [Theory]
    [InlineData("crossbowman", 0.25)]
    [InlineData("hexcaster", 0.05)]
    [InlineData("dire-wolf", 0.30)]
    public void Deliberately_tuned_units_keep_their_own_variance(string id, double expected)
    {
        Assert.Equal(expected, Catalog.Get(id).DamageVariance);
    }

    [Fact]
    public void Every_other_unit_uses_the_default_variance()
    {
        // The criterion is "0.15 unless deliberately tuned otherwise", and
        // nothing was checking it against the roster: a typo giving conscript
        // 0.5 passed the whole suite. Naming the exceptions is what makes
        // this an assertion rather than a restatement of whatever is there.
        string[] tuned = ["crossbowman", "hexcaster", "dire-wolf"];

        string[] offenders =
        [
            .. Catalog.All
                .Where(d => !tuned.Contains(d.Id, StringComparer.Ordinal))
                .Where(d => d.DamageVariance != UnitDefinition.DefaultDamageVariance)
                .Select(d => $"{d.Id}={d.DamageVariance}")
        ];

        Assert.Empty(offenders);
    }

    [Fact]
    public void Only_the_enemy_only_monsters_are_non_player_selectable()
    {
        // The human roster stays fully player-facing; only the hand-picked
        // monster trio should ever be filtered out of the picker.
        string[] enemyOnly = ["goblin", "dire-wolf", "ogre"];

        foreach (UnitDefinition definition in Catalog.All)
        {
            bool expectedSelectable = !enemyOnly.Contains(definition.Id, StringComparer.Ordinal);
            Assert.Equal(expectedSelectable, definition.PlayerSelectable);
        }
    }

    [Fact]
    public void Includes_both_melee_and_ranged_units()
    {
        Assert.Contains(Catalog.All, d => d.AttackRangeHexes == 1);
        Assert.Contains(Catalog.All, d => d.AttackRangeHexes > 1);
    }

    [Fact]
    public void Enumerates_in_source_order()
    {
        // Pinned against a literal sequence, so a reordered, sorted or
        // truncated roster fails here. Anything downstream that iterates the
        // roster to reach an outcome — a draft pool, a shop roll — needs an
        // order fixed by the source.
        //
        // What this does NOT catch, checked by mutation rather than assumed:
        // rewriting All as [.. _byId.Values] still passes, because .NET's
        // Dictionary keeps its entries in an insertion-ordered array and this
        // catalog never removes one. That order is not contractual and has
        // changed between runtimes, which is why the assignment reads from
        // `definitions`; but no runtime assertion here can tell the two apart
        // today, and pretending otherwise would make this the kind of test that
        // only looks like a guarantee.
        string[] expected =
        [
            "conscript", "knight", "berserker", "archer", "crossbowman", "hexcaster",
            "goblin", "dire-wolf", "ogre",
        ];

        Assert.Equal(expected, Catalog.All.Select(d => d.Id));
    }

    [Fact]
    public void Rejects_duplicate_ids_at_construction()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            new StaticUnitCatalog([Unit("conscript"), Unit("archer", range: 3), Unit("conscript")]));

        Assert.Contains("conscript", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Accepts_a_roster_with_distinct_ids()
    {
        var catalog = new StaticUnitCatalog([Unit("a"), Unit("b"), Unit("c")]);

        Assert.Equal(3, catalog.All.Count);
        Assert.Equal(["a", "b", "c"], catalog.All.Select(d => d.Id));
    }

    [Fact]
    public void A_non_selectable_unit_is_still_reachable_through_the_catalog()
    {
        // PlayerSelectable gates the player-facing roster endpoint only - the
        // catalog itself must keep resolving every unit for server-authored
        // placements like enemy setups.
        UnitDefinition hidden = Unit("griffon", playerSelectable: false);
        var catalog = new StaticUnitCatalog([Unit("a"), hidden]);

        Assert.Same(hidden, catalog.Get("griffon"));
        Assert.Contains(hidden, catalog.All);
    }

    [Fact]
    public void Later_changes_to_the_source_list_do_not_leak_into_the_catalog()
    {
        // The catalog copies its input; a caller keeping the list must not be
        // able to add a unit to a catalog after it was validated.
        List<UnitDefinition> source = [Unit("a"), Unit("b")];
        var catalog = new StaticUnitCatalog(source);

        source.Add(Unit("c"));

        Assert.Equal(2, catalog.All.Count);
    }
}
