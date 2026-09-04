namespace GameSim;

/// <summary>Every <see cref="UnitDefinition"/> the game knows, looked up by id. The rest of the simulation depends on this interface, never on <see cref="StaticUnitCatalog"/> directly.</summary>
public interface IUnitCatalog
{
    /// <summary>A list, deliberately: .NET guarantees no iteration order for a dictionary, and anything reaching an outcome needs one.</summary>
    IReadOnlyList<UnitDefinition> All { get; }

    /// <exception cref="KeyNotFoundException">Asking for an id that doesn't exist is a programmer error.</exception>
    UnitDefinition Get(string id);
}
