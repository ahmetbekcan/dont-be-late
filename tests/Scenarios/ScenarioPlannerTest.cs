namespace DontBeLate.Tests.Scenarios;

using System.Linq;
using DontBeLate.Scenarios;
using GdUnit4;
using ReusableGameSystems.Pools;
using ReusableGameSystems.Randomness;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public class ScenarioPlannerTest
{
    private static readonly int[] Counts = { 1, 2, 3 };

    private static ContentPool Pool() => new()
    {
        Entries =
        {
            new ScenarioDefinition { Id = "rain", Tags = new[] { "weather" }, ExcludesTags = new[] { "weather" } },
            new ScenarioDefinition { Id = "snow", Tags = new[] { "weather" } },
            new ScenarioDefinition { Id = "cat_on_shoes" },
            new ScenarioDefinition { Id = "neighbor" },
        },
    };

    [TestCase]
    public void CountGrowsWithRunsAndRepeatsLastValue()
    {
        var random = new RandomStream(1);
        AssertInt(ScenarioPlanner.ScenarioCount(1, Counts, 0, 0, random)).IsEqual(1);
        AssertInt(ScenarioPlanner.ScenarioCount(2, Counts, 0, 0, random)).IsEqual(2);
        AssertInt(ScenarioPlanner.ScenarioCount(9, Counts, 0, 0, random)).IsEqual(3);
    }

    [TestCase]
    public void NormalMorningsOnlyAfterGuaranteedRuns()
    {
        var random = new RandomStream(1);
        AssertInt(ScenarioPlanner.ScenarioCount(2, Counts, 1.0, 2, random)).IsEqual(2);
        AssertInt(ScenarioPlanner.ScenarioCount(3, Counts, 1.0, 2, random)).IsEqual(0);
    }

    [TestCase]
    public void SameSeedSamePlan()
    {
        var a = ScenarioPlanner.Plan(Pool(), 3, Counts, 0.2, 1, System.Array.Empty<string>(), null, new RandomStream(42));
        var b = ScenarioPlanner.Plan(Pool(), 3, Counts, 0.2, 1, System.Array.Empty<string>(), null, new RandomStream(42));
        AssertArray(a.Select(s => s.Id).ToList()).IsEqual(b.Select(s => s.Id).ToList());
    }

    [TestCase]
    public void ForcedScenariosAreIncludedEvenOnNormalMornings()
    {
        var plan = ScenarioPlanner.Plan(Pool(), 5, new[] { 0 }, 1.0, 0, new[] { "cat_on_shoes" }, null, new RandomStream(1));
        AssertArray(plan.Select(s => s.Id).ToList()).ContainsExactly("cat_on_shoes");
    }

    [TestCase]
    public void WeatherScenariosNeverStack()
    {
        for (ulong seed = 0; seed < 40; seed++)
        {
            var ids = ScenarioPlanner.Plan(Pool(), 3, Counts, 0, 0, System.Array.Empty<string>(), null, new RandomStream(seed)).Select(s => s.Id).ToList();
            AssertBool(ids.Contains("rain") && ids.Contains("snow")).IsFalse();
        }
    }

    [TestCase]
    public void QueueHandsOutForcedIdsOnce()
    {
        ScenarioQueue.Clear();
        ScenarioQueue.Force("rain");
        ScenarioQueue.Force("rain");
        ScenarioQueue.Force("cat_on_shoes");

        AssertArray(ScenarioQueue.Take()).ContainsExactly("rain", "cat_on_shoes");
        AssertInt(ScenarioQueue.Pending.Count).IsEqual(0);
    }

    [TestCase]
    public void ShippedPoolLoadsAndHasBothHomeScenarios()
    {
        var pool = Godot.GD.Load<ContentPool>("res://content/scenarios/home_pool.tres");
        var ids = pool.Entries.OfType<ScenarioDefinition>().Select(s => s.Id).ToList();
        AssertArray(ids).ContainsExactlyInAnyOrder("rain", "cat_on_shoes");
        AssertBool(pool.Entries.OfType<ScenarioDefinition>().Single(s => s.Id == "rain").Facts.ContainsKey("raining")).IsTrue();
    }
}
