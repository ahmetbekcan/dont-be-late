using System;
using System.Collections.Generic;
using System.Linq;
using DontBeLate.DebugCommands;
using DontBeLate.Scenarios;
using Godot;
using ReusableGameSystems.Clock;
using ReusableGameSystems.Consequences;
using ReusableGameSystems.Events;
using ReusableGameSystems.Interaction;
using ReusableGameSystems.Pools;
using ReusableGameSystems.Randomness;
using ReusableGameSystems.Runs;
using ReusableGameSystems.UI;
using ReusableGameSystems.WorldState;

namespace DontBeLate.Stages;

/// <summary>
/// Runs one morning in a stage scene: starts the run, resets the clock and world, draws the morning's scenarios
/// and applies them, finishes the stage at the front door, and shows the fail / success screen. Continuing reloads
/// the scene, which starts the next morning.
/// </summary>
[GlobalClass]
public partial class MorningDirector : Node
{
    private static int _lastSetUpRun;

    private readonly List<ScenarioDefinition> _active = new();
    private RunManager? _runs;
    private bool _setUp;
    private bool _reloading;

    [Signal]
    public delegate void MorningStartedEventHandler(int runNumber);

    /// <summary>The director of the stage currently loaded.</summary>
    public static MorningDirector? Current { get; private set; }

    [ExportGroup("Scenarios")]
    [Export]
    public ContentPool? ScenarioPool { get; set; }

    /// <summary>Rules every morning has (e.g. the bus leaves at 8:00). Applied like a scenario, never drawn.</summary>
    [Export]
    public ScenarioDefinition? BaseRules { get; set; }

    /// <summary>How many scenarios run 1, 2, 3... get; the last value repeats.</summary>
    [Export]
    public int[] ScenarioCountsByRun { get; set; } = { 1, 1, 2, 2, 3 };

    /// <summary>Chance that a morning has no scenario at all ("normal mornings exist").</summary>
    [Export(PropertyHint.Range, "0,1,0.01")]
    public double NormalMorningChance { get; set; } = 0.15;

    /// <summary>The first runs always have at least one scenario, so new players see the idea.</summary>
    [Export(PropertyHint.Range, "0,10,1")]
    public int GuaranteedScenarioRuns { get; set; } = 2;

    /// <summary>Clock time the morning starts at (later: set by the wake-up mini game).</summary>
    [ExportGroup("Morning")]
    [Export]
    public string StartTime { get; set; } = "07:30";

    [Export(PropertyHint.MultilineText)]
    public string StartMessage { get; set; } = "7:30. The bus leaves at 8:00. Don't be late.";

    [ExportGroup("Scene")]
    [Export]
    public WorldStateStore? World { get; set; }

    [Export]
    public ConsequenceSystem? Consequences { get; set; }

    /// <summary>Using <see cref="LeaveVerb"/> on it ends the stage.</summary>
    [Export]
    public Interactable? FrontDoor { get; set; }

    [Export]
    public string LeaveVerb { get; set; } = "leave";

    /// <summary>Location reported to the consequence system when the player leaves.</summary>
    [Export]
    public string FrontDoorLocation { get; set; } = "front_door";

    [Export]
    public EndScreen? EndScreen { get; set; }

    public IReadOnlyList<ScenarioDefinition> ActiveScenarios => _active;

    public override void _Ready()
    {
        Current = this;
        ScenarioCommands.Register();

        if (FrontDoor != null)
        {
            FrontDoor.InteractedWith += OnDoorUsed;
        }

        if (EndScreen != null)
        {
            EndScreen.Continued += ReloadStage;
        }

        _runs = RunManager.Instance;
        if (_runs == null)
        {
            GD.PushError("[MorningDirector] No RunManager autoload found.");
            return;
        }

        _runs.Loop.Started += OnRunStarted;
        _runs.Loop.Ended += OnRunEnded;

        var loop = _runs.Loop;
        if (loop.IsRunning && loop.RunNumber != _lastSetUpRun)
        {
            // A run was started (e.g. from the console) and this fresh scene should play it.
            SetUpMorning();
        }
        else
        {
            _runs.StartRun();
        }
    }

    public override void _ExitTree()
    {
        if (_runs != null)
        {
            _runs.Loop.Started -= OnRunStarted;
            _runs.Loop.Ended -= OnRunEnded;
        }

        if (FrontDoor != null && IsInstanceValid(FrontDoor))
        {
            FrontDoor.InteractedWith -= OnDoorUsed;
        }

        if (Current == this)
        {
            Current = null;
        }
    }

    /// <summary>Abandons the current morning and starts a new one (reloads the stage).</summary>
    public void Restart() => ReloadStage();

    private void OnRunStarted(RunStarted started)
    {
        if (_setUp)
        {
            // The world of this scene was already used; play the new run in a fresh copy.
            ReloadStage();
        }
        else
        {
            SetUpMorning();
        }
    }

    private void SetUpMorning()
    {
        var loop = _runs!.Loop;
        _setUp = true;
        _lastSetUpRun = loop.RunNumber;

        if (GameClock.Instance is { } clock)
        {
            clock.Keeper.Reset(ClockTime.Parse(StartTime));
            clock.Paused = false;
        }

        World?.ResetToInitial();
        Consequences?.ClearAll();

        _active.Clear();
        if (ScenarioPool != null)
        {
            var random = RandomService.Instance?.Stream("scenarios") ?? new RandomStream(loop.Seed);
            _active.AddRange(ScenarioPlanner.Plan(
                ScenarioPool,
                loop.RunNumber,
                ScenarioCountsByRun,
                NormalMorningChance,
                GuaranteedScenarioRuns,
                ScenarioQueue.Take(),
                Consequences?.Context,
                random));
        }

        if (BaseRules != null)
        {
            Apply(BaseRules);
        }

        foreach (var scenario in _active)
        {
            Apply(scenario);
        }

        EventBus.Instance?.EmitEvent("toast", StartMessage);
        EmitSignal(SignalName.MorningStarted, loop.RunNumber);
    }

    private void Apply(ScenarioDefinition scenario)
    {
        World?.Facts.SetMany(scenario.Facts);
        foreach (var consequence in scenario.Consequences)
        {
            if (consequence != null)
            {
                Consequences?.Register(consequence);
            }
        }
    }

    private void OnDoorUsed(Interactable door, InteractionVerb verb, Node? actor)
    {
        if (verb.Id != LeaveVerb || _runs == null || !_runs.Loop.IsRunning)
        {
            return;
        }

        // Anything that was waiting for the player to step outside happens now (it may fail the run).
        Consequences?.ReportLocation(FrontDoorLocation);
        if (_runs.Loop.IsRunning)
        {
            string time = GameClock.Instance?.Now.ToString() ?? "";
            _runs.CompleteRun($"Out the door at {time}");
        }
    }

    private void OnRunEnded(RunEnded ended)
    {
        if (GameClock.Instance is { } clock)
        {
            clock.Paused = true;
        }

        Consequences?.ClearAll();
        if (EndScreen == null)
        {
            return;
        }

        switch (ended.Outcome)
        {
            case RunOutcome.Failed:
                EndScreen.ShowScreen("Not today.", $"{ended.Reason}\n\nMorning #{ended.RunNumber}", "Back to bed");
                break;
            case RunOutcome.Completed:
                EndScreen.ShowScreen(
                    "Out the door!",
                    $"{ended.Reason}.\nThe street, the bus and the office are still to come.\n\nMorning #{ended.RunNumber}",
                    "Another morning");
                break;
        }
    }

    private void ReloadStage()
    {
        if (_reloading)
        {
            return;
        }

        _reloading = true;
        Callable.From(() => GetTree().ReloadCurrentScene()).CallDeferred();
    }

    /// <summary>Text for the debug overlay: active scenarios and any forced for next time.</summary>
    public string DescribeScenarios()
    {
        string active = _active.Count == 0 ? "(normal morning)" : string.Join(", ", _active.Select(s => s.Id));
        return ScenarioQueue.Pending.Count == 0 ? active : $"{active}  next: {string.Join(", ", ScenarioQueue.Pending)}";
    }
}
