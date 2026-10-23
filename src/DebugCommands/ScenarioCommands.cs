using System.Linq;
using System.Text;
using DontBeLate.Scenarios;
using DontBeLate.Stages;
using ReusableGameSystems.DebugTools;

namespace DontBeLate.DebugCommands;

/// <summary>Console commands for scenarios: list, inspect, force for the next morning, restart the morning.</summary>
public static class ScenarioCommands
{
    /// <summary>Registers the commands and watches. Safe to call again (commands are replaced).</summary>
    public static void Register()
    {
        var console = DebugConsole.Instance;
        if (console != null)
        {
            Register(console.Commands);
        }

        DebugOverlay.Instance?.Watch("Scenarios", () => MorningDirector.Current?.DescribeScenarios() ?? "-");
    }

    public static void Register(CommandRegistry registry)
    {
        registry.Register("scenario", "Lists, shows or forces scenarios. Forced ones apply from the next morning.", args =>
        {
            var director = MorningDirector.Current ?? throw new CommandException("No stage with a morning director is loaded.");
            var pool = director.ScenarioPool ?? throw new CommandException("The director has no scenario pool.");
            switch (args.GetString(0, "active").ToLowerInvariant())
            {
                case "list":
                    return string.Join('\n', pool.Entries.OfType<ScenarioDefinition>().Select(s => $"{s.Id,-16}{s.DisplayName}  (weight {s.Weight})"));

                case "active":
                {
                    if (director.ActiveScenarios.Count == 0)
                    {
                        return "Normal morning: nothing is different.";
                    }

                    var text = new StringBuilder();
                    foreach (var scenario in director.ActiveScenarios)
                    {
                        text.Append(scenario.Id).Append(": ").Append(scenario.DisplayName).Append('\n')
                            .Append("  clue: ").Append(scenario.Clue).Append('\n')
                            .Append("  means: ").Append(scenario.Meaning).Append('\n')
                            .Append("  do: ").Append(scenario.Requirement).Append('\n');
                    }

                    return text.ToString().TrimEnd();
                }

                case "force":
                {
                    if (!args.Has(1))
                    {
                        throw args.UsageError();
                    }

                    for (int i = 1; i < args.Count; i++)
                    {
                        if (pool.FindById(args[i]) == null)
                        {
                            throw new CommandException($"No scenario '{args[i]}'. Try 'scenario list'.");
                        }

                        ScenarioQueue.Force(args[i]);
                    }

                    return $"forced for next morning: {string.Join(", ", ScenarioQueue.Pending)}  (type 'restart' to start it now)";
                }

                case "clear":
                    ScenarioQueue.Clear();
                    return "no scenarios forced";
                default:
                    throw args.UsageError();
            }
        }, "scenario [active | list | force <id> [id...] | clear]");

        registry.Register("restart", "Abandons this morning and starts a new one.", _ =>
        {
            var director = MorningDirector.Current ?? throw new CommandException("No stage with a morning director is loaded.");
            director.Restart();
            return "restarting...";
        });
    }
}
