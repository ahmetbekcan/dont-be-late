using System.Collections.Generic;

namespace DontBeLate.Scenarios;

/// <summary>Scenarios forced for the next morning (from the debug console). Survives scene reloads.</summary>
public static class ScenarioQueue
{
    private static readonly List<string> Forced = new();

    public static IReadOnlyList<string> Pending => Forced;

    public static void Force(string scenarioId)
    {
        if (!Forced.Contains(scenarioId))
        {
            Forced.Add(scenarioId);
        }
    }

    /// <summary>Returns the forced ids and empties the queue.</summary>
    public static List<string> Take()
    {
        var ids = new List<string>(Forced);
        Forced.Clear();
        return ids;
    }

    public static void Clear() => Forced.Clear();
}
