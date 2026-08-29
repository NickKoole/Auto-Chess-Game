namespace GameSim;

/// <summary>
/// The simulation's vocabulary of units: every <see cref="UnitDefinition"/> the
/// game knows about, looked up by id.
/// <para>
/// Definitions are hardcoded today. Loading them from a data file is a second
/// implementation of this interface, which is why the rest of the simulation
/// depends on the interface and never on the static catalog directly.
/// </para>
/// </summary>
public interface IUnitCatalog
{
    /// <summary>
    /// Every definition, in a fixed order.
    /// <para>
    /// Deliberately a list rather than an <see cref="IEnumerable{T}"/> over a
    /// dictionary: .NET guarantees no iteration order for a dictionary and has
    /// changed it between runtimes, so anything that reaches an outcome has to
    /// come from an ordered collection.
    /// </para>
    /// </summary>
    IReadOnlyList<UnitDefinition> All { get; }

    /// <summary>
    /// The definition with this id.
    /// </summary>
    /// <exception cref="KeyNotFoundException">
    /// No definition has this id. Asking for a unit that does not exist is a
    /// programmer error, so it throws rather than returning null.
    /// </exception>
    UnitDefinition Get(string id);
}
