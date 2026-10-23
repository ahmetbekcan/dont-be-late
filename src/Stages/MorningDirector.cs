using System;
using System.Collections.Generic;
using System.Linq;
using DontBeLate.DebugCommands;
using DontBeLate.Scenarios;
using Godot;
using ReusableGameSystems.Clock;
using ReusableGameSystems.Consequences;
using ReusableGameSystems.Events;
using ReusableGameSystems.Conditions;
using ReusableGameSystems.Items;
using ReusableGameSystems.Meters;
using ReusableGameSystems.Pools;
using ReusableGameSystems.Randomness;
using ReusableGameSystems.Runs;
using ReusableGameSystems.StatusEffects;
using ReusableGameSystems.UI;
using ReusableGameSystems.WorldState;

namespace DontBeLate.Stages;

/// <summary>
/// Runs one morning in a stage scene: starts the run, resets the clock and world, draws the morning's scenarios
/// and applies them, ends the morning when the player gets on a bus (or misses the last one), and shows the fail / success screen. Continuing reloads
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
    public string StartMessage { get; set; } = "7:30. Work starts at 9:00. Buses at 7:45, 8:00 and 8:10.";

    /// <summary>Meter id used to describe how wet the player is on the end screen.</summary>
    [Export]
    public string WetnessMeter { get; set; } = "wetness";

    [ExportGroup("Scene")]
    [Export]
    public WorldStateStore? World { get; set; }

    [Export]
    public ConsequenceSystem? Consequences { get; set; }

    /// <summary>The buses. Getting on one ends the morning; missing the last one that's on time fails it.</summary>
    [Export]
    public BusService? Buses { get; set; }

    [Export]
    public EndScreen? EndScreen { get; set; }

    public IReadOnlyList<ScenarioDefinition> ActiveScenarios => _active;

    public override void _Ready()
    {
        Current = this;
        ScenarioCommands.Register();
        BusCommands.Register();

        if (Buses != null)
        {
            Buses.Boarded += OnBoarded;
            Buses.LastOnTimeBusMissed += OnLastBusMissed;
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

        if (Buses != null && IsInstanceValid(Buses))
        {
            Buses.Boarded -= OnBoarded;
            Buses.LastOnTimeBusMissed -= OnLastBusMissed;
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

    private void OnBoarded(int bus, string departure, string arrivalAtWork, bool onTime)
    {
        if (_runs == null || !_runs.Loop.IsRunning)
        {
            return;
        }

        if (onTime)
        {
            _runs.CompleteRun($"You caught the {departure} bus. You'll be at work by {arrivalAtWork}.{DescribePlayer()}");
        }
        else
        {
            _runs.FailRun($"You caught the {departure} bus, but you won't be at work until {arrivalAtWork}.{DescribePlayer()}");
        }
    }

    private void OnLastBusMissed(string departure)
    {
        if (_runs != null && _runs.Loop.IsRunning)
        {
            _runs.FailRun($"The {departure} bus left without you. It was the last one that gets you to work by 9:00.{DescribePlayer()}");
        }
    }

    /// <summary>How the player looks right now ("wet socks, scratched, soaked"), for the end screen.</summary>
    private string DescribePlayer()
    {
        var parts = new List<string>();
        if (LogicContext.GetGlobal<MeterSet>()?.Get(WetnessMeter) is { } wetness)
        {
            if (wetness.Value >= 60)
            {
                parts.Add("soaked");
            }
            else if (wetness.Value >= 25)
            {
                parts.Add("damp");
            }
        }

        if (LogicContext.GetGlobal<StatusEffectSet>() is { } effects)
        {
            foreach (var effect in effects.Active)
            {
                if (!string.IsNullOrEmpty(effect.Definition.DisplayName))
                {
                    parts.Add(effect.Definition.DisplayName.ToLowerInvariant());
                }
            }
        }

        if (LogicContext.GetGlobal<Inventory>()?.GetEquipped("feet") is { } feet)
        {
            parts.Add($"in {feet.Definition.DisplayName.ToLowerInvariant()}");
        }
        else
        {
            parts.Add("barefoot");
        }

        return $"\nYou look: {string.Join(", ", parts.Distinct())}.";
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
                    "On the bus!",
                    $"{ended.Reason}\n(The ride and the office are still to come.)\n\nMorning #{ended.RunNumber}",
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
