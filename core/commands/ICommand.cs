namespace EpochsOfHumanity.Core.Commands;

/// <summary>
/// Marker interface for player/AI actions.
/// </summary>
/// <remarks>
/// Law 2: UI does not mutate the world directly. All actions go through ICommand objects
/// queued for execution at the start of the next tick. This is also the foundation for
/// future lockstep multiplayer (only commands are synced between clients).
/// See <c>.claude/skills/game-architecture/SKILL.md</c>.
/// </remarks>
public interface ICommand
{
    /// <summary>Strategic tick when the command was issued.</summary>
    long IssuedAtTick { get; }
}

/// <summary>Result of validating a command before execution.</summary>
public readonly record struct CommandValidation(bool Ok, string? ReasonKey)
{
    public static CommandValidation Valid => new(true, null);
    public static CommandValidation Invalid(string reasonKey) => new(false, reasonKey);
}
