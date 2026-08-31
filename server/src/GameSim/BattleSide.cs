namespace GameSim;

/// <summary>
/// Which army a unit belongs to.
///
/// The numbering is load-bearing: it is the first key in the deterministic unit
/// order — which then runs front-to-back within each army and finally by column
/// — so renumbering these members reorders every battle and changes who wins a
/// mutual kill. Where each side may deploy, and which way it advances, are
/// properties of the board rather than of the side, and live on
/// <see cref="Battlefield"/>.
/// </summary>
public enum BattleSide
{
    /// <summary>The human player. Holds the bottom of the board and advances north.</summary>
    Player = 0,

    /// <summary>The opposing army. Holds the top of the board and advances south.</summary>
    Opponent = 1,
}
