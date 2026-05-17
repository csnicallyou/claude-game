using EpochsOfHumanity.Core.Prng;
using EpochsOfHumanity.Core.Time;

namespace EpochsOfHumanity.Core.Era;

/// <summary>
/// Limited API given to an era during initialization.
/// Era cannot access render, ui, commands or save through this context.
/// </summary>
public interface IEraInitContext
{
    GameClock Clock { get; }
    Rng Rng { get; }
    // World access added when GameWorld is implemented in v0.1
    // IContentRegistry Content { get; }
}

/// <summary>
/// Limited API given to an era on every tick.
/// </summary>
public interface IEraTickContext
{
    GameClock Clock { get; }
    long Tick { get; }
    Rng Rng { get; }
    // IEventBus Events { get; }
    // IContentRegistry Content { get; }  // read-only here
}

/// <summary>
/// API given to an era for registering its content (technologies, events, biomes).
/// World does not yet exist at this point — only content registries.
/// </summary>
public interface IEraContentContext
{
    // ITechRegistry TechTree { get; }
    // IEventRegistry Events { get; }
    // IBiomeRegistry Biomes { get; }
    // ISpeciesRegistry Species { get; }
    // IWorldviewRegistry Worldviews { get; }
    // IDataLoader Data { get; }
}

/// <summary>
/// API given to an era on save load to restore its specific state.
/// </summary>
public interface IEraRehydrateContext
{
    GameClock Clock { get; }
    // IContentRegistry Content { get; }
}
