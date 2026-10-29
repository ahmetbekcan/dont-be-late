using System.Collections.Generic;
using DontBeLate.Stages;
using Godot;
using ReusableGameSystems.CityGen;
using ReusableGameSystems.CityLife;
using ReusableGameSystems.Clock;
using ReusableGameSystems.Phone;
using ReusableGameSystems.Transit;

namespace DontBeLate.PhoneApps;

/// <summary>
/// Live bus times. On the street (<see cref="BusService"/>): each bus, when it leaves, when it gets you to work,
/// whether that's on time, and how long until it comes (or that it's gone). In a generated town
/// (<see cref="TransitLife"/>): the stop nearest you and the next buses of each line calling there.
/// </summary>
public partial class BusesAppView : PhoneAppView
{
    private readonly List<Label> _status = new();
    private Label _headline = null!;
    private int _shownMinute = -2;

    public string StopName { get; set; } = "Your stop";

    /// <summary>The status line of each bus (for tests).</summary>
    public IReadOnlyList<Label> StatusLabels => _status;

    public string Headline => _headline.Text;

    /// <summary>The stop shown in a generated town, or null.</summary>
    public TransitStop? Stop { get; private set; }

    /// <summary>Next buses shown per line at <see cref="Stop"/>, a line each (for tests).</summary>
    public IReadOnlyList<Label> LineLabels => _lines;

    private readonly List<Label> _lines = new();
    private VBoxContainer? _list;

    /// <summary>How many coming buses to list per line.</summary>
    private const int Upcoming = 3;

    public override void OnOpened()
    {
        var list = AddList();
        AddLabel(list, StopName, PhoneScreen.MutedVariation);
        _headline = AddLabel(list, "", PhoneScreen.TitleVariation);
        var buses = BusService.Current;
        if (buses == null)
        {
            _list = list;
            if (TransitLife.Current?.Sim != null)
            {
                RefreshTown();
                return;
            }

            _headline.Text = "No bus stop nearby.";
            return;
        }

        var timetable = buses.Timetable;
        for (int i = 0; i < timetable.Count; i++)
        {
            var card = AddCard(list);
            var row = new HBoxContainer();
            card.AddChild(row);
            var time = AddLabel(row, timetable.Arrivals[i].ToString(), PhoneScreen.BigVariation);
            time.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
            time.AutowrapMode = TextServer.AutowrapMode.Off;
            var right = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
            row.AddChild(right);
            bool onTime = timetable.IsOnTime(i);
            var work = AddLabel(right, $"At work {timetable.ArrivalAtWork(i)}  {(onTime ? "on time" : "LATE")}", PhoneScreen.MutedVariation);
            work.HorizontalAlignment = HorizontalAlignment.Right;
            if (!onTime)
            {
                work.AddThemeColorOverride("font_color", new Color(1f, 0.45f, 0.4f));
            }

            var status = AddLabel(right, "", PhoneScreen.AccentVariation);
            status.HorizontalAlignment = HorizontalAlignment.Right;
            _status.Add(status);
        }

        Refresh();
    }

    public override void OnShown() => Refresh();

    public override void _Process(double delta)
    {
        // Updates every game minute (and every few seconds near a bus, for "at the stop").
        int minute = GameClock.Instance is { } clock ? (int)(clock.Now.TotalSeconds / 20) : -1;
        if (minute != _shownMinute)
        {
            _shownMinute = minute;
            Refresh();
        }
    }

    /// <summary>Recomputes every line from the clock.</summary>
    public void Refresh()
    {
        if (BusService.Current == null && TransitLife.Current?.Sim != null)
        {
            RefreshTown();
            return;
        }

        if (BusService.Current is not { } buses || GameClock.Instance is not { } clock)
        {
            return;
        }

        var timetable = buses.Timetable;
        var now = clock.Now;
        var phase = timetable.PhaseAt(now, out int here, out _);
        for (int i = 0; i < _status.Count; i++)
        {
            _status[i].Text = StatusOf(timetable, i, now, i == here ? phase : BusPhase.Away);
        }

        int next = timetable.NextBus(now);
        int last = timetable.LastOnTimeBus;
        _headline.Text = next < 0
            ? "No more buses today."
            : next > last
                ? "No bus gets you there by " + timetable.WorkStarts + "."
                : $"Next: {timetable.Arrivals[next]}, {Until(now, timetable.Arrivals[next])}";
    }

    /// <summary>The stop nearest you in a generated town and the next buses of each line there.</summary>
    private void RefreshTown()
    {
        if (TransitLife.Current is not { Sim: { } sim } transit || _list == null)
        {
            return;
        }

        var target = CitySimulation.Current?.Target;
        var here = target != null && IsInstanceValid(target) ? CityLayout.ToPlan(target.GlobalPosition) : Vector2.Zero;
        var stop = sim.NearestStop(here);
        if (stop == null)
        {
            _headline.Text = "No bus stop nearby.";
            return;
        }

        // Moved to another stop: new cards.
        if (stop != Stop)
        {
            Stop = stop;
            foreach (var label in _lines)
            {
                label.GetParent()?.QueueFree();
            }

            _lines.Clear();
            foreach (var line in stop.Lines)
            {
                var card = AddCard(_list);
                var index = line.IndexOf(stop);
                var towards = index + 1 < line.Stops.Count ? line.Stops[^1].Name : line.Stops[0].Name;
                AddLabel(card, $"{line.Name} to {towards}", PhoneScreen.BigVariation);
                _lines.Add(AddLabel(card, "", PhoneScreen.AccentVariation));
            }
        }

        double now = transit.Time;
        _headline.Text = $"{stop.Name}, {Mathf.RoundToInt(here.DistanceTo(stop.WaitPosition) / 10) * 10} m away";
        for (int i = 0; i < stop.Lines.Count && i < _lines.Count; i++)
        {
            var line = stop.Lines[i];
            int index = line.IndexOf(stop);
            bool atStop = false;
            foreach (var bus in sim.Shown)
            {
                atStop |= bus.Line == line && bus.Stop == stop && !double.IsNaN(bus.LeavesAt);
            }

            var times = new List<string>();
            double after = now;
            for (int n = 0; n < Upcoming; n++)
            {
                double next = line.NextAt(index, after);
                if (double.IsPositiveInfinity(next))
                {
                    break;
                }

                var clock = new ClockTime(next % 86400);
                int minutes = (int)System.Math.Ceiling((next - now) / 60.0);
                times.Add(n == 0 ? $"{clock} ({(minutes <= 0 ? "now" : $"in {minutes} min")})" : clock.ToString());
                after = next + 1;
            }

            _lines[i].Text = atStop ? "At the stop!" : times.Count > 0 ? string.Join("  ·  ", times) : "No more buses today.";
        }
    }

    private static string StatusOf(BusTimetable timetable, int bus, ClockTime now, BusPhase phase) => phase switch
    {
        BusPhase.Approaching => "Arriving now",
        BusPhase.AtStop => "At the stop!",
        BusPhase.Departing => "Leaving...",
        _ => now >= timetable.DepartureOf(bus) ? "Gone" : Until(now, timetable.Arrivals[bus]),
    };

    private static string Until(ClockTime now, ClockTime then)
    {
        int minutes = (int)System.Math.Ceiling(now.SecondsUntil(then) / 60.0);
        return minutes <= 0 ? "now" : minutes == 1 ? "in 1 min" : $"in {minutes} min";
    }
}
