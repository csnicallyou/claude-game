namespace EpochsOfHumanity.Core.Events;

/// <summary>
/// Marker interface for objective events emitted by the simulation.
/// </summary>
/// <remarks>
/// These are "what happened" — pure facts. The perception layer (Law 4) interprets
/// them through each character's worldview into <c>PerceivedEvent</c>. See
/// <c>.claude/skills/game-perception/SKILL.md</c>.
///
/// Use <c>sealed record</c>-based discriminated unions, never string-typed events.
/// </remarks>
public interface IObjectiveEvent
{
    /// <summary>Globally-unique event id, deterministic.</summary>
    string Id { get; }

    /// <summary>Strategic tick when the event occurred.</summary>
    long Tick { get; }
}
