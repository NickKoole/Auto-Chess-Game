namespace GameSim;

/// <summary>
/// The numbering is load-bearing: it is the first key in the deterministic
/// unit order (then front-to-back, then column), so renumbering these
/// members reorders every battle and changes who wins a mutual kill.
/// Deployment rows and advance direction are <see cref="Battlefield"/>'s
/// properties, not this type's.
/// </summary>
public enum BattleSide
{
    Player = 0,

    Opponent = 1,
}
