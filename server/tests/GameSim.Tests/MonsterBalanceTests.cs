namespace GameSim.Tests;

/// <summary>
/// Property tests for the enemy-only monster trio: not exact damage, but the
/// property each unit is meant to have - "Goblin is weaker than Conscript",
/// "Ogre is tankier and hits harder than Knight" - checked across many seeds
/// rather than asserted once against whatever the sim happens to return.
/// </summary>
public sealed class MonsterBalanceTests
{
    private const int Trials = 100;
    private const int RequiredWins = 80;

    private static readonly StaticUnitCatalog Catalog = new();

    private static int CountWins(string attackerId, string defenderId, BattleOutcome winFor)
    {
        UnitDefinition attacker = Catalog.Get(attackerId);
        UnitDefinition defender = Catalog.Get(defenderId);

        int wins = 0;

        for (ulong seed = 1; seed <= Trials; seed++)
        {
            Assert.True(BattleSetup.TryCreate(
                [new UnitPlacement(attacker, new HexCoord(3, 4))],
                [new UnitPlacement(defender, new HexCoord(3, 7))],
                out BattleSetup? setup,
                out IReadOnlyList<PlacementError> errors));
            Assert.Empty(errors);

            var simulation = new BattleSimulation(setup!, seed);
            BattleResult result = simulation.Run();

            if (result.Outcome == winFor)
            {
                wins++;
            }
        }

        return wins;
    }

    [Fact]
    public void Conscript_beats_a_solo_goblin_in_a_clear_majority_of_seeds()
    {
        int conscriptWins = CountWins(attackerId: "conscript", defenderId: "goblin", winFor: BattleOutcome.PlayerWin);

        Assert.True(
            conscriptWins >= RequiredWins,
            $"Expected Conscript to beat a solo Goblin in at least {RequiredWins}/{Trials} seeds, but it won {conscriptWins}.");
    }

    [Fact]
    public void Ogre_beats_a_solo_knight_in_a_clear_majority_of_seeds()
    {
        int ogreWins = CountWins(attackerId: "ogre", defenderId: "knight", winFor: BattleOutcome.PlayerWin);

        Assert.True(
            ogreWins >= RequiredWins,
            $"Expected Ogre to beat a solo Knight in at least {RequiredWins}/{Trials} seeds, but it won {ogreWins}.");
    }
}
