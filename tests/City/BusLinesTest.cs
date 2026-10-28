namespace DontBeLate.Tests.City;

using System.Linq;
using System.Threading.Tasks;
using DontBeLate.City;
using GdUnit4;
using Godot;
using ReusableGameSystems.CityGen;
using ReusableGameSystems.Tests;
using ReusableGameSystems.Transit;
using static GdUnit4.Assertions;

/// <summary>The town's buses (content/transit): every city gets its lines, and buses run in the preview.</summary>
[TestSuite]
[RequireGodotRuntime]
public class BusLinesTest
{
    private const string TestPath = "user://test_bus_lines.tres";

    [TestCase]
    public void EveryCityHasItsBusLines()
    {
        var style = GD.Load<CityStyle>("res://content/city/city_style.tres");
        var settings = GD.Load<TransitSettings>("res://content/transit/transit.tres");
        for (ulong seed = 1; seed <= 5; seed++)
        {
            var sim = new TransitSim(CityGenerator.Generate(style, seed), settings, seed);
            AssertInt(sim.Lines.Count).OverrideFailureMessage($"city {seed}").IsEqual(2);
            AssertThat(sim.Lines.Select(l => l.Name).ToArray()).IsEqual(new[] { "Number 9", "Number 12" });
            foreach (var line in sim.Lines)
            {
                AssertInt(line.Stops.Count).OverrideFailureMessage($"{line} in city {seed}").IsGreaterEqual(4);
                AssertFloat((float)(line.RoundTrip / 60)).OverrideFailureMessage($"{line} round in city {seed}").IsBetween(3f, 40f);
            }

            AssertBool(sim.Stops.All(s => s.Name.Length > 0)).IsTrue();
        }
    }

    [TestCase]
    public async Task BusesRunInThePreview()
    {
        CityStore.Path = TestPath;
        DirAccess.RemoveAbsolute(TestPath);
        try
        {
            var tree = (SceneTree)Engine.GetMainLoop();
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            var scene = GD.Load<PackedScene>("res://scenes/stages/city/city_preview.tscn").Instantiate();
            using var runner = ISceneRunner.Load(scene, autoFree: true);
            await runner.Step(5);
            var transit = scene.GetNode<TransitLife>("Stage/Life/Transit");
            var sim = transit.Sim!;
            AssertInt(sim.Lines.Count).IsEqual(2);

            // Wait at a stop on the pavement: a bus comes along.
            var stop = sim.Lines[0].Stops[1];
            var player = scene.GetNode<Node3D>("Player");
            player.GlobalPosition = CityLayout.ToWorld(sim.Layout.Walk.Nodes[stop.WalkNode].Position, 1f);
            double wait = sim.Lines[0].NextAt(1, transit.Time) - transit.Time;
            for (int i = 0; i < 60 && sim.ShownCount == 0; i++)
            {
                await runner.StepSeconds(1);
            }

            AssertInt(sim.ShownCount).OverrideFailureMessage($"no bus in a minute (next due in {wait:0} s)").IsGreater(0);
        }
        finally
        {
            DirAccess.RemoveAbsolute(TestPath);
            CityStore.Path = CityStore.SavePath;
        }
    }
}
