using System;
using System.Collections.Generic;
using System.Linq;
using ReusableGameSystems.Conditions;
using ReusableGameSystems.Pools;
using ReusableGameSystems.Randomness;

namespace DontBeLate.Scenarios;

/// <summary>Decides which scenarios a morning gets: how many (grows with the run number), and which (weighted draw).</summary>
public static class ScenarioPlanner
{
    /// <summary>
    /// How many scenarios to draw for a run. The first <paramref name="guaranteedRuns"/> runs always get some; after
    /// that a morning is "normal" (nothing wrong) with <paramref name="normalMorningChance"/>.
    /// </summary>
    public static int ScenarioCount(int runNumber, IReadOnlyList<int> countsByRun, double normalMorningChance, int guaranteedRuns, RandomStream random)
    {
        // Always roll, so the draws that follow don't depend on whether this run was guaranteed.
        bool normal = random.Chance(normalMorningChance);
        if (normal && runNumber > guaranteedRuns)
        {
            return 0;
        }

        if (countsByRun.Count == 0)
        {
            return 1;
        }

        return Math.Max(0, countsByRun[Math.Clamp(runNumber - 1, 0, countsByRun.Count - 1)]);
    }

    public static List<ScenarioDefinition> Plan(
        ContentPool pool,
        int runNumber,
        IReadOnlyList<int> countsByRun,
        double normalMorningChance,
        int guaranteedRuns,
        IReadOnlyCollection<string> forcedIds,
        LogicContext? context,
        RandomStream random)
    {
        int count = ScenarioCount(runNumber, countsByRun, normalMorningChance, guaranteedRuns, random);
        var request = new PoolDrawRequest
        {
            Count = Math.Max(count, forcedIds.Count),
            Difficulty = runNumber - 1,
            Context = context,
        };

        foreach (string id in forcedIds)
        {
            request.ForcedIds.Add(id);
        }

        return pool.Draw(request, random).OfType<ScenarioDefinition>().ToList();
    }
}
