using System;
using System.Collections.Generic;
using ReusableGameSystems.Clock;

namespace DontBeLate.Stages;

/// <summary>Where a bus is in its visit to the stop.</summary>
public enum BusPhase
{
    /// <summary>Not on the street.</summary>
    Away,

    /// <summary>Driving up to the stop.</summary>
    Approaching,

    /// <summary>Standing at the stop with the door open.</summary>
    AtStop,

    /// <summary>Pulling away.</summary>
    Departing,
}

/// <summary>
/// The buses of one stop: when each one arrives, how long it waits, and whether riding it gets you to work on
/// time. Plain C#, so the schedule can be tested without a scene.
/// </summary>
public sealed class BusTimetable
{
    private readonly List<ClockTime> _arrivals = new();

    public BusTimetable(IEnumerable<ClockTime> arrivals, double approachMinutes, double dwellMinutes, double departMinutes, double rideMinutes, ClockTime workStarts)
    {
        _arrivals.AddRange(arrivals);
        _arrivals.Sort();
        ApproachMinutes = Math.Max(0, approachMinutes);
        DwellMinutes = Math.Max(0, dwellMinutes);
        DepartMinutes = Math.Max(0, departMinutes);
        RideMinutes = Math.Max(0, rideMinutes);
        WorkStarts = workStarts;
    }

    public IReadOnlyList<ClockTime> Arrivals => _arrivals;

    public double ApproachMinutes { get; }

    public double DwellMinutes { get; }

    public double DepartMinutes { get; }

    public double RideMinutes { get; }

    public ClockTime WorkStarts { get; }

    public int Count => _arrivals.Count;

    public ClockTime DepartureOf(int bus) => _arrivals[bus].AddMinutes(DwellMinutes);

    /// <summary>When you get to work if you ride bus <paramref name="bus"/>.</summary>
    public ClockTime ArrivalAtWork(int bus) => DepartureOf(bus).AddMinutes(RideMinutes);

    public bool IsOnTime(int bus) => ArrivalAtWork(bus) <= WorkStarts;

    /// <summary>The last bus that still gets you to work on time, or -1 if none does.</summary>
    public int LastOnTimeBus
    {
        get
        {
            for (int i = _arrivals.Count - 1; i >= 0; i--)
            {
                if (IsOnTime(i))
                {
                    return i;
                }
            }

            return -1;
        }
    }

    /// <summary>The bus that is on the street at <paramref name="now"/> (or -1), its phase and progress (0..1) through that phase.</summary>
    public BusPhase PhaseAt(ClockTime now, out int bus, out double progress)
    {
        for (int i = 0; i < _arrivals.Count; i++)
        {
            var arrive = _arrivals[i];
            var appearAt = arrive.AddMinutes(-ApproachMinutes);
            var leaveAt = DepartureOf(i);
            var goneAt = leaveAt.AddMinutes(DepartMinutes);
            if (now < appearAt || now >= goneAt)
            {
                continue;
            }

            bus = i;
            if (now < arrive)
            {
                progress = Fraction(appearAt, arrive, now);
                return BusPhase.Approaching;
            }

            if (now < leaveAt)
            {
                progress = Fraction(arrive, leaveAt, now);
                return BusPhase.AtStop;
            }

            progress = Fraction(leaveAt, goneAt, now);
            return BusPhase.Departing;
        }

        bus = -1;
        progress = 0;
        return BusPhase.Away;
    }

    /// <summary>The next bus that hasn't left yet at <paramref name="now"/>, or -1.</summary>
    public int NextBus(ClockTime now)
    {
        for (int i = 0; i < _arrivals.Count; i++)
        {
            if (now < DepartureOf(i))
            {
                return i;
            }
        }

        return -1;
    }

    private static double Fraction(ClockTime from, ClockTime to, ClockTime now)
    {
        double span = from.SecondsUntil(to);
        return span <= 0 ? 1 : Math.Clamp(from.SecondsUntil(now) / span, 0, 1);
    }
}
