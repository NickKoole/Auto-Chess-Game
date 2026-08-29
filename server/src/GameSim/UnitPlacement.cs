namespace GameSim;

/// <summary>
/// A request to deploy one unit on one tile: the definition to instantiate and
/// where it should stand.
/// <para>
/// It carries the definition itself rather than a unit id, so building a setup
/// needs no <see cref="IUnitCatalog"/> and cannot fail on an unknown id. Turning
/// a persisted id into a definition is the API boundary's job, where a lookup
/// failure is already a typed failure.
/// </para>
/// </summary>
/// <param name="Definition">The kind of unit to deploy.</param>
/// <param name="Coord">The tile it should stand on.</param>
public readonly record struct UnitPlacement(UnitDefinition Definition, HexCoord Coord);
