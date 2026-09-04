namespace GameSim;

/// <summary>Carries the definition itself, not an id - building a setup needs no <see cref="IUnitCatalog"/>. Turning a persisted id into a definition is the API boundary's job.</summary>
public readonly record struct UnitPlacement(UnitDefinition Definition, HexCoord Coord);
