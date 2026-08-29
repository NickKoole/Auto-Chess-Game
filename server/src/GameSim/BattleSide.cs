namespace GameSim;

/// <summary>
/// Which army a unit belongs to.
/// <para>
/// The numbering is load-bearing: it is the first key in the deterministic unit
/// order, so renumbering these members reorders every battle and changes who
/// wins a mutual kill. Where each side may deploy is a property of the board
/// rather than of the side, and lives on <see cref="Battlefield"/>.
/// </para>
/// </summary>
public enum BattleSide
{
    /// <summary>The human player. Holds the bottom of the board and advances north.</summary>
    Player = 0,

    /// <summary>The opposing army. Holds the top of the board and advances south.</summary>
    Opponent = 1,
}
