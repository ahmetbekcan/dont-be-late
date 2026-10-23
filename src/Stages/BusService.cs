using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using ReusableGameSystems.Clock;
using ReusableGameSystems.Conditions;
using ReusableGameSystems.Events;
using ReusableGameSystems.Interaction;

namespace DontBeLate.Stages;

/// <summary>
/// The buses at the stop. Each bus drives in on schedule, waits with its door open, and leaves, whether you're
/// on it or not. Its position comes from the game clock, so skipping time moves it too. Trying to get on asks
/// the driver, who checks <see cref="Rules"/> (soaked, barefoot...) and may refuse you.
/// </summary>
[GlobalClass]
public partial class BusService : Node
{
    private readonly List<ClockTime> _arrivals = new();
    private BusTimetable? _timetable;
    private LogicContext? _context;
    private int _lastBus = -1;
    private BusPhase _lastPhase = BusPhase.Away;
    private bool _lastOnTimeGone;

    /// <summary>The player got on bus <paramref name="bus"/>; they reach work at <paramref name="arrivalAtWork"/> ("H:mm").</summary>
    [Signal]
    public delegate void BoardedEventHandler(int bus, string departure, string arrivalAtWork, bool onTime);

    [Signal]
    public delegate void RefusedEventHandler(string ruleId, string driverLine);

    [Signal]
    public delegate void BusArrivedEventHandler(int bus);

    [Signal]
    public delegate void BusDepartedEventHandler(int bus);

    /// <summary>The last bus that gets the player to work on time left without them.</summary>
    [Signal]
    public delegate void LastOnTimeBusMissedEventHandler(string departure);

    [ExportGroup("Timetable")]
    [Export]
    public string[] Schedule { get; set; } = { "07:45", "08:00", "08:10" };

    /// <summary>Game minutes the bus takes to drive in from the end of the street.</summary>
    [Export(PropertyHint.Range, "0,10,0.05")]
    public double ApproachMinutes { get; set; } = 0.4;

    /// <summary>Game minutes the bus waits at the stop.</summary>
    [Export(PropertyHint.Range, "0,10,0.05")]
    public double DwellMinutes { get; set; } = 1;

    [Export(PropertyHint.Range, "0,10,0.05")]
    public double DepartMinutes { get; set; } = 0.4;

    /// <summary>Game minutes from the stop to work.</summary>
    [Export(PropertyHint.Range, "0,180,1")]
    public double RideMinutes { get; set; } = 49;

    [Export]
    public string WorkStarts { get; set; } = "09:00";

    [ExportGroup("Scene")]
    /// <summary>The bus body that drives along the street (moved along its local X).</summary>
    [Export]
    public Node3D? Bus { get; set; }

    /// <summary>Interactable on the bus door; enabled only while the bus is at the stop.</summary>
    [Export]
    public Interactable? Door { get; set; }

    [Export]
    public string BoardVerb { get; set; } = "board";

    /// <summary>X where the bus stands at the stop, where it appears, and where it disappears.</summary>
    [Export]
    public float StopX { get; set; } = 38f;

    [Export]
    public float EntryX { get; set; } = -30f;

    [Export]
    public float ExitX { get; set; } = 110f;

    [ExportGroup("Driver")]
    [Export]
    public Godot.Collections.Array<BoardingRule> Rules { get; set; } = new();

    [Export]
    public string DriverName { get; set; } = "Driver";

    public static BusService? Current { get; private set; }

    public BusTimetable Timetable => _timetable ??= BuildTimetable();

    public bool HasBoarded { get; private set; }

    public LogicContext Context
    {
        get => _context ??= LogicContext.FromGlobals("bus");
        set => _context = value;
    }

    public override void _Ready()
    {
        Current = this;
        if (Door != null)
        {
            Door.InteractedWith += OnDoorUsed;
            Door.Enabled = false;
        }

        UpdateBus();
    }

    public override void _ExitTree()
    {
        if (Door != null)
        {
            Door.InteractedWith -= OnDoorUsed;
        }

        if (Current == this)
        {
            Current = null;
        }
    }

    public override void _PhysicsProcess(double delta) => UpdateBus();

    /// <summary>Asks the driver to let the player on. Only works while a bus is at the stop.</summary>
    public bool TryBoard()
    {
        var now = GameClock.Instance?.Now;
        if (now == null || HasBoarded || Timetable.PhaseAt(now.Value, out int bus, out _) != BusPhase.AtStop)
        {
            return false;
        }

        var accepted = new List<string>();
        foreach (var rule in Rules)
        {
            if (rule == null || !(rule.When?.Evaluate(Context) ?? false))
            {
                continue;
            }

            if (rule.Refuse)
            {
                Say(rule.DriverLine);
                EmitSignal(SignalName.Refused, rule.Id, rule.DriverLine);
                return false;
            }

            accepted.Add(rule.DriverLine);
        }

        foreach (string line in accepted)
        {
            Say(line);
        }

        HasBoarded = true;
        if (Door != null)
        {
            Door.Enabled = false;
        }

        EmitSignal(
            SignalName.Boarded,
            bus,
            Timetable.DepartureOf(bus).ToString(),
            Timetable.ArrivalAtWork(bus).ToString(),
            Timetable.IsOnTime(bus));
        return true;
    }

    /// <summary>One line per bus for the debug console.</summary>
    public string Describe()
    {
        var now = GameClock.Instance?.Now ?? default;
        var phase = Timetable.PhaseAt(now, out int current, out _);
        return string.Join('\n', Timetable.Arrivals.Select((arrival, i) =>
            $"{arrival}  leaves {Timetable.DepartureOf(i)}  at work {Timetable.ArrivalAtWork(i)}{(Timetable.IsOnTime(i) ? "" : " (late)")}{(i == current ? $"  <- {phase}" : "")}"));
    }

    private BusTimetable BuildTimetable()
    {
        _arrivals.Clear();
        foreach (string time in Schedule)
        {
            if (ClockTime.TryParse(time, out var parsed))
            {
                _arrivals.Add(parsed);
            }
            else
            {
                GD.PushWarning($"[BusService] Bad time '{time}' in the schedule.");
            }
        }

        var workStarts = ClockTime.TryParse(WorkStarts, out var work) ? work : ClockTime.FromHms(9, 0);
        return new BusTimetable(_arrivals, ApproachMinutes, DwellMinutes, DepartMinutes, RideMinutes, workStarts);
    }

    private void UpdateBus()
    {
        var clock = GameClock.Instance;
        if (clock == null)
        {
            return;
        }

        var phase = Timetable.PhaseAt(clock.Now, out int bus, out double progress);
        PlaceBus(phase, progress);
        if (Door != null)
        {
            Door.Enabled = phase == BusPhase.AtStop && !HasBoarded;
        }

        if (bus != _lastBus || phase != _lastPhase)
        {
            OnPhaseChanged(_lastBus, _lastPhase, bus, phase);
            _lastBus = bus;
            _lastPhase = phase;
        }

        // Also catches time jumps that skip a bus entirely.
        int lastOnTime = Timetable.LastOnTimeBus;
        if (!_lastOnTimeGone && !HasBoarded && lastOnTime >= 0 && clock.Now >= Timetable.DepartureOf(lastOnTime))
        {
            _lastOnTimeGone = true;
            EmitSignal(SignalName.LastOnTimeBusMissed, Timetable.DepartureOf(lastOnTime).ToString());
        }
    }

    private void OnPhaseChanged(int oldBus, BusPhase oldPhase, int bus, BusPhase phase)
    {
        if (oldBus >= 0 && oldPhase == BusPhase.AtStop && (bus != oldBus || phase != BusPhase.AtStop))
        {
            EmitSignal(SignalName.BusDeparted, oldBus);
        }

        if (bus < 0)
        {
            return;
        }

        if (phase == BusPhase.Approaching && !HasBoarded)
        {
            EventBus.Instance?.EmitEvent("toast", $"The {Timetable.Arrivals[bus]} bus is coming down the street.");
        }
        else if (phase == BusPhase.AtStop)
        {
            EmitSignal(SignalName.BusArrived, bus);
        }
    }

    private void PlaceBus(BusPhase phase, double progress)
    {
        if (Bus == null)
        {
            return;
        }

        float t = (float)progress;
        float x = phase switch
        {
            BusPhase.Approaching => Mathf.Lerp(EntryX, StopX, 1 - ((1 - t) * (1 - t))),
            BusPhase.AtStop => StopX,
            BusPhase.Departing => Mathf.Lerp(StopX, ExitX, t * t),
            _ => EntryX,
        };

        Bus.Visible = phase != BusPhase.Away;
        var position = Bus.Position;
        position.X = x;
        position.Y = phase == BusPhase.Away ? -50 : 0;
        Bus.Position = position;
    }

    private void Say(string line)
    {
        if (!string.IsNullOrEmpty(line))
        {
            EventBus.Instance?.EmitEvent("toast", $"{DriverName}: \"{line}\"");
        }
    }

    private void OnDoorUsed(Interactable door, InteractionVerb verb, Node? actor)
    {
        if (verb.Id == BoardVerb)
        {
            TryBoard();
        }
    }
}
