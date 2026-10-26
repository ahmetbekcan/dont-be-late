using System.Text;
using DontBeLate.City;
using ReusableGameSystems.CityGen;
using ReusableGameSystems.DebugTools;
using ReusableGameSystems.Randomness;

namespace DontBeLate.DebugCommands;

/// <summary>Console commands for the generated city: show it, make a new one, keep it, pick by seed, go to landmarks.</summary>
public static class CityCommands
{
    /// <summary>Registers the commands and the overlay watch. Safe to call again.</summary>
    public static void Register()
    {
        if (DebugConsole.Instance is { } console)
        {
            Register(console.Commands);
        }

        DebugOverlay.Instance?.Watch("City", () =>
        {
            var stage = CityStage.Current;
            return stage == null ? "-" : $"{stage.SeedCode} ({stage.Settings.Mode})";
        });
    }

    public static void Register(CommandRegistry registry)
    {
        registry.Register("city", "Shows the city, or makes a new one, keeps it, builds one from a seed, sets the seed mode, goes to a landmark or shows paths.", args =>
        {
            var stage = CityStage.Current ?? throw new CommandException("No generated city in this stage.");
            switch (args.GetString(0, "info").ToLowerInvariant())
            {
                case "info":
                    return Describe(stage);
                case "new":
                    stage.NewCity();
                    return $"new city {stage.SeedCode}";
                case "seed":
                    stage.Build(Seeds.Parse(args.GetString(1)));
                    return $"city {stage.SeedCode}";
                case "keep":
                    stage.KeepCurrent();
                    return $"keeping city {stage.SeedCode} for every run";
                case "mode":
                {
                    var mode = args.GetString(1).ToLowerInvariant() switch
                    {
                        "run" or "new" => CitySeedMode.NewEachRun,
                        "kept" or "keep" => CitySeedMode.Kept,
                        _ => throw args.UsageError(),
                    };
                    stage.SetMode(mode);
                    return $"city mode {mode}";
                }

                case "goto":
                {
                    string id = args.GetString(1);
                    return stage.MovePlayerTo(id) ? $"at {id}" : throw new CommandException($"No landmark '{id}'. Try 'city' for the list.");
                }

                case "paths":
                    if (stage.City != null)
                    {
                        stage.City.ShowNavigation = !stage.City.ShowNavigation;
                        return stage.City.ShowNavigation ? "showing walkways and lanes" : "paths hidden";
                    }

                    return "no city";
                default:
                    throw args.UsageError();
            }
        }, "city [info | new | seed <code> | keep | mode run|kept | goto <landmark> | paths]");
    }

    private static string Describe(CityStage stage)
    {
        var layout = stage.Layout;
        if (layout == null)
        {
            return "no city built yet";
        }

        var text = new StringBuilder();
        text.Append($"city {stage.SeedCode}, mode {stage.Settings.Mode}");
        if (stage.Settings.Mode == CitySeedMode.Kept)
        {
            text.Append($" (kept {stage.Settings.KeptSeed})");
        }

        text.Append($"\n{layout.Lots.Count} lots, {layout.Signals.Count} junctions with lights, {layout.Seats.Count} seats");
        foreach (var landmark in layout.Landmarks)
        {
            var p = layout.LandmarkWorldPosition(landmark);
            text.Append($"\n  {landmark.Id}: {landmark.DisplayName} at ({p.X:0}, {p.Z:0})");
        }

        return text.ToString();
    }
}
