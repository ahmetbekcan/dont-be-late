using DontBeLate.Stages;
using ReusableGameSystems.Clock;
using ReusableGameSystems.DebugTools;

namespace DontBeLate.DebugCommands;

/// <summary>Console commands for the buses: timetable, skip to the next bus, try to board.</summary>
public static class BusCommands
{
    /// <summary>Registers the commands and the overlay watch. Safe to call again.</summary>
    public static void Register()
    {
        if (DebugConsole.Instance is { } console)
        {
            Register(console.Commands);
        }

        DebugOverlay.Instance?.Watch("Bus", () =>
        {
            var buses = BusService.Current;
            if (buses == null || GameClock.Instance is not { } clock)
            {
                return "-";
            }

            var phase = buses.Timetable.PhaseAt(clock.Now, out int bus, out _);
            int next = buses.Timetable.NextBus(clock.Now);
            return bus >= 0
                ? $"{buses.Timetable.Arrivals[bus]} {phase}"
                : next >= 0 ? $"next {buses.Timetable.Arrivals[next]}" : "no more buses";
        });
    }

    public static void Register(CommandRegistry registry)
    {
        registry.Register("bus", "Shows the bus timetable, jumps to just before the next bus, or tries to board.", args =>
        {
            var buses = BusService.Current ?? throw new CommandException("No bus stop in this stage.");
            switch (args.GetString(0, "list").ToLowerInvariant())
            {
                case "list":
                    return buses.Describe();
                case "next":
                {
                    var clock = GameClock.Instance ?? throw new CommandException("No clock.");
                    int next = buses.Timetable.NextBus(clock.Now);
                    if (next < 0)
                    {
                        throw new CommandException("No more buses today.");
                    }

                    var target = buses.Timetable.Arrivals[next].AddMinutes(-buses.Timetable.ApproachMinutes - 0.25);
                    if (target > clock.Now)
                    {
                        clock.Keeper.SetTime(target);
                    }

                    return $"time {clock.Now}; the {buses.Timetable.Arrivals[next]} bus is about to come";
                }

                case "board":
                    return buses.TryBoard() ? "boarded" : "couldn't board (no bus at the stop, or the driver said no)";
                default:
                    throw args.UsageError();
            }
        }, "bus [list | next | board]");
    }
}
