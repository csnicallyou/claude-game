namespace EpochsOfHumanity.Core.Era;

/// <summary>
/// Contract for an era module. Each era (Paleolithic, Mesolithic, ...) implements this.
/// </summary>
/// <remarks>
/// Law 3: era = module. Core does not know about specific eras. Eras import only
/// the core API and shared sim API via contexts. Eras NEVER import other eras.
/// See <c>.claude/skills/game-eras/SKILL.md</c>.
/// </remarks>
public interface IEraModule
{
    /// <summary>Unique stable identifier, e.g. "paleolithic", "mesolithic".</summary>
    string Id { get; }

    /// <summary>Localization key for the era's display name (not a literal string).</summary>
    string DisplayNameKey { get; }

    /// <summary>Years BP range this era covers.</summary>
    TimeRange TimeRange { get; }

    /// <summary>Called once when a new world is created if this era is active at start.</summary>
    void Init(IEraInitContext ctx);

    /// <summary>Called every strategic tick. Pure sim logic — no Godot, no IO.</summary>
    void Tick(IEraTickContext ctx);

    /// <summary>
    /// Registers era content (technologies, events, biomes, worldviews) into the content registry.
    /// Called once after <see cref="Init"/>, before the first <see cref="Tick"/>.
    /// </summary>
    void Content(IEraContentContext ctx);

    /// <summary>
    /// Called on save load instead of <see cref="Init"/> to restore era-specific state from snapshot.
    /// </summary>
    void Rehydrate(IEraRehydrateContext ctx, EraSnapshot snapshot);
}

/// <summary>Opaque snapshot of an era's specific state, used by save/load.</summary>
public sealed record EraSnapshot(string EraId, System.Collections.Generic.IReadOnlyDictionary<string, object> Data);
