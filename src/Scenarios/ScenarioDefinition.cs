using Godot;
using ReusableGameSystems.Consequences;
using ReusableGameSystems.Pools;

namespace DontBeLate.Scenarios;

/// <summary>
/// One "something is different this morning" scenario: the clue the player can spot, what it means, and what they
/// must do. Drawn from a pool (weight, tags, exclusions, difficulty come from <see cref="PoolEntry"/>). When active,
/// it sets <see cref="Facts"/> (which switch on the clues and obstacles in the level) and registers its
/// <see cref="Consequences"/> (what happens if the requirement isn't met).
/// </summary>
[GlobalClass]
public partial class ScenarioDefinition : PoolEntry
{
    [Export]
    public string DisplayName { get; set; } = "";

    /// <summary>What the player can notice, e.g. "Puddles outside the window".</summary>
    [Export(PropertyHint.MultilineText)]
    public string Clue { get; set; } = "";

    /// <summary>What the clue means, e.g. "It rained overnight". Shown in the journal once discovered.</summary>
    [Export(PropertyHint.MultilineText)]
    public string Meaning { get; set; } = "";

    /// <summary>What the player has to do, e.g. "Take an umbrella and boots".</summary>
    [Export(PropertyHint.MultilineText)]
    public string Requirement { get; set; } = "";

    /// <summary>Blackboard facts set when the morning starts (e.g. raining = true).</summary>
    [Export]
    public Godot.Collections.Dictionary Facts { get; set; } = new();

    /// <summary>Registered when the morning starts.</summary>
    [Export]
    public Godot.Collections.Array<ConsequenceDefinition> Consequences { get; set; } = new();
}
