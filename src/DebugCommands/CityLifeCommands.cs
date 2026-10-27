using System.Collections.Generic;
using System.Text;
using DontBeLate.City;
using Godot;
using ReusableGameSystems.CityGen;
using ReusableGameSystems.CityLife;
using ReusableGameSystems.DebugTools;

namespace DontBeLate.DebugCommands;

/// <summary>Console commands for traffic, people and signals in the generated city.</summary>
public static class CityLifeCommands
{
    private static int _carsWhenOn = -1;
    private static int _walkersWhenOn = -1;

    /// <summary>Registers the commands and the overlay watch. Safe to call again.</summary>
    public static void Register()
    {
        if (DebugConsole.Instance is { } console)
        {
            Register(console.Commands);
        }

        DebugOverlay.Instance?.Watch("Traffic", () =>
            CitySimulation.Current?.Sim is { } sim ? $"{sim.Cars.Count} cars, {sim.Walkers.Count} people" : "-");
    }

    public static void Register(CommandRegistry registry)
    {
        registry.Register("traffic", "Traffic: on/off, how many cars, the mix of drivers, or make the nearest car crazy.", args =>
        {
            var sim = Sim();
            switch (args.GetString(0, "info").ToLowerInvariant())
            {
                case "info":
                    return $"{sim.Cars.Count} of {sim.MaxCars} cars";
                case "on":
                    sim.MaxCars = _carsWhenOn > 0 ? _carsWhenOn : sim.Traffic.MaxCars;
                    return $"traffic on ({sim.MaxCars} cars)";
                case "off":
                    _carsWhenOn = sim.MaxCars;
                    sim.MaxCars = 0;
                    sim.ClearCars();
                    return "traffic off";
                case "count":
                    sim.MaxCars = Mathf.Max(0, args.GetInt(1));
                    return $"{sim.MaxCars} cars";
                case "drivers":
                    return Drivers(sim);
                case "crazy":
                {
                    var player = PlayerPlan() ?? throw new CommandException("No player.");
                    var car = sim.NearestCar(player) ?? throw new CommandException("No cars.");
                    DriverProfile? crazy = null;
                    foreach (var profile in sim.Traffic.Drivers)
                    {
                        if (profile?.Id == "crazy")
                        {
                            crazy = profile;
                        }
                    }

                    var driver = crazy?.Roll(sim.Random) ?? new DriverTraits { Profile = "crazy", SpeedFactor = 1.5f, RunRedChance = 1, RunAmberChance = 1, PlayerAwareness = 0, HonkPatience = 0 };
                    sim.ReplaceDriver(car, driver);
                    return $"the car {car.Position.DistanceTo(player):0} m away now has a crazy driver";
                }

                default:
                    throw args.UsageError();
            }
        }, "traffic [info | on | off | count <n> | drivers | crazy]");

        registry.Register("crowd", "People: on/off, how many, what kinds, what the nearest one is up to, or make them do something.", args =>
        {
            var sim = Sim();
            switch (args.GetString(0, "info").ToLowerInvariant())
            {
                case "info":
                {
                    int sitting = 0;
                    int waiting = 0;
                    int busy = 0;
                    foreach (var walker in sim.Walkers)
                    {
                        sitting += walker.Activity == WalkerActivity.Sitting ? 1 : 0;
                        waiting += walker.Activity == WalkerActivity.WaitingToCross ? 1 : 0;
                        busy += walker.Activity == WalkerActivity.Busy ? 1 : 0;
                    }

                    return $"{sim.Walkers.Count} of {sim.MaxWalkers} people ({sitting} sitting, {waiting} waiting to cross, {busy} busy)";
                }

                case "kinds":
                    return Kinds(sim);
                case "labels":
                {
                    var labels = Labels(true)!;
                    labels.Visible = args.GetString(1, labels.Visible ? "off" : "on").ToLowerInvariant() switch
                    {
                        "on" => true,
                        "off" => false,
                        _ => throw args.UsageError(),
                    };
                    return labels.Visible ? $"labels on (people within {labels.Range:0} m)" : "labels off";
                }

                case "why":
                    return Why(sim, NearestWalker(sim));
                case "do":
                {
                    string id = args.GetString(1, "");
                    var activity = sim.FindActivity(id) ?? throw new CommandException($"No activity '{id}'. Activities: {ActivityIds(sim)}");
                    var walker = NearestWalker(sim);
                    return sim.StartActivity(walker, activity)
                        ? $"walker {walker.Id} ({walker.Position.DistanceTo(PlayerPlan()!.Value):0} m away): {id}"
                        : $"walker {walker.Id} can't {id} right now (on a crossing, or nowhere or nobody near)";
                }

                case "on":
                    sim.MaxWalkers = _walkersWhenOn > 0 ? _walkersWhenOn : sim.Crowd.MaxWalkers;
                    return $"crowd on ({sim.MaxWalkers} people)";
                case "off":
                    _walkersWhenOn = sim.MaxWalkers;
                    sim.MaxWalkers = 0;
                    sim.ClearWalkers();
                    return "crowd off";
                case "count":
                    sim.MaxWalkers = Mathf.Max(0, args.GetInt(1));
                    return $"{sim.MaxWalkers} people";
                default:
                    throw args.UsageError();
            }
        }, "crowd [info | on | off | count <n> | kinds | why | do <activity> | labels [on | off]]");

        registry.Register("signals", "Shows the traffic lights at the junction nearest you.", _ =>
        {
            var layout = CityBuilder.Current?.Layout ?? throw new CommandException("No generated city.");
            var player = PlayerPlan() ?? throw new CommandException("No player.");
            double time = CityBuilder.Current!.SignalTime;
            IntersectionSignal? nearest = null;
            float best = float.MaxValue;
            foreach (var signal in layout.Signals)
            {
                float distance = layout.Graph.Nodes[signal.Node].Position.DistanceTo(player);
                if (distance < best)
                {
                    best = distance;
                    nearest = signal;
                }
            }

            if (nearest == null)
            {
                return "no traffic lights in this city";
            }

            return $"junction {best:0} m away: group 0 {nearest.StateAt(0, time)}, group 1 {nearest.StateAt(1, time)}; " +
                $"next change in {nearest.TimeToChange(0, time):0.0} s";
        });

        registry.Register("honk", "Makes the honk and hit reactions happen (honk | hit [speed]).", args =>
        {
            var reactions = Reactions() ?? throw new CommandException("No traffic reactions in this stage.");
            if (args.GetString(0, "honk").ToLowerInvariant() == "hit")
            {
                float speed = (float)args.GetDouble(1, 10);
                reactions.HitPlayer(speed, Vector2.Right);
                return $"hit at {speed} m/s";
            }

            reactions.Honk(0);
            return "honk";
        }, "honk [hit [speed]]");
    }

    private static CityLifeSim Sim() =>
        CitySimulation.Current?.Sim ?? throw new CommandException("No traffic in this stage.");

    private static Vector2? PlayerPlan() =>
        (Engine.GetMainLoop() as SceneTree)?.GetFirstNodeInGroup("player") is Node3D player ? CityLayout.ToPlan(player.GlobalPosition) : null;

    /// <summary>The labels under the simulation (made, hidden, on first use).</summary>
    private static CrowdLabels? Labels(bool make)
    {
        var life = CitySimulation.Current ?? throw new CommandException("No traffic in this stage.");
        foreach (var child in life.GetChildren())
        {
            if (child is CrowdLabels found)
            {
                return found;
            }
        }

        if (!make)
        {
            return null;
        }

        var labels = new CrowdLabels { Name = "CrowdLabels", Visible = false };
        life.AddChild(labels);
        return labels;
    }

    private static WalkerState NearestWalker(CityLifeSim sim)
    {
        var player = PlayerPlan() ?? throw new CommandException("No player.");
        return sim.NearestWalker(player) ?? throw new CommandException("No people.");
    }

    /// <summary>What a walker is up to and why.</summary>
    private static string Why(CityLifeSim sim, WalkerState walker)
    {
        var traits = walker.Traits;
        var text = new StringBuilder($"walker {walker.Id}, a {traits.Profile} ({traits.WalkSpeed:0.0} m/s, curiosity {traits.Curiosity:0.00}, {traits.StopsPerMinute:0.0} stops/min): ");
        text.Append(walker.Activity switch
        {
            WalkerActivity.Busy => $"{walker.Doing?.Id ?? "busy"} for {walker.TimeLeft:0} s more" + (walker.Partner is { } p ? $" with walker {p.Id}" : ""),
            WalkerActivity.Sitting => $"sitting for {walker.TimeLeft:0} s more",
            WalkerActivity.Idle => $"standing about for {walker.TimeLeft:0.0} s more",
            WalkerActivity.WaitingToCross => "waiting to cross",
            WalkerActivity.Blocked => "blocked by you",
            _ => walker.Goal == WalkerGoal.Building ? $"on the way to {walker.Doing?.Id}" : $"walking to a {walker.Goal.ToString().ToLowerInvariant()}",
        });
        if (walker.Gesture.Length > 0)
        {
            text.Append($"; gesture {walker.Gesture}");
        }

        if (walker.LookAt is { } look)
        {
            text.Append(PlayerPlan() is { } player && look.DistanceTo(player) < 0.5f ? "; looking at you" : $"; looking at {look.X:0},{look.Y:0}");
        }

        return text.ToString();
    }

    private static string Kinds(CityLifeSim sim)
    {
        var counts = new SortedDictionary<string, int>();
        foreach (var walker in sim.Walkers)
        {
            counts.TryGetValue(walker.Traits.Profile, out int n);
            counts[walker.Traits.Profile] = n + 1;
        }

        var text = new StringBuilder($"{sim.Walkers.Count} people:");
        foreach (var (profile, count) in counts)
        {
            text.Append($" {count} {profile},");
        }

        return text.ToString().TrimEnd(',') + $". Activities: {ActivityIds(sim)}";
    }

    private static string ActivityIds(CityLifeSim sim)
    {
        var ids = new List<string>();
        foreach (var activity in sim.Crowd.Activities)
        {
            if (activity != null)
            {
                ids.Add(activity.Id);
            }
        }

        return ids.Count > 0 ? string.Join(", ", ids) : "none";
    }

    private static TrafficReactions? Reactions()
    {
        var tree = Engine.GetMainLoop() as SceneTree;
        return tree?.CurrentScene?.FindChild("TrafficReactions", true, false) as TrafficReactions;
    }

    private static string Drivers(CityLifeSim sim)
    {
        var counts = new SortedDictionary<string, int>();
        foreach (var car in sim.Cars)
        {
            counts.TryGetValue(car.Driver.Profile, out int n);
            counts[car.Driver.Profile] = n + 1;
        }

        var text = new StringBuilder($"{sim.Cars.Count} cars:");
        foreach (var (profile, count) in counts)
        {
            text.Append($" {count} {profile},");
        }

        return text.ToString().TrimEnd(',');
    }
}
