using System.Collections.Generic;
using DontBeLate.Stages;
using Godot;
using ReusableGameSystems.Clock;
using ReusableGameSystems.Phone;

namespace DontBeLate.PhoneApps;

/// <summary>
/// Live bus times from the street's <see cref="BusService"/>: each bus, when it leaves, when it gets you to work,
/// whether that's on time, and how long until it comes (or that it's gone).
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

    public override void OnOpened()
    {
        var list = AddList();
        AddLabel(list, StopName, PhoneScreen.MutedVariation);
        _headline = AddLabel(list, "", PhoneScreen.TitleVariation);
        var buses = BusService.Current;
        if (buses == null)
        {
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
