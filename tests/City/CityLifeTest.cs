namespace DontBeLate.Tests.City;

using System.Threading.Tasks;
using DontBeLate.City;
using GdUnit4;
using Godot;
using ReusableGameSystems.Characters;
using ReusableGameSystems.CityLife;
using ReusableGameSystems.StatusEffects;
using ReusableGameSystems.Tests;
using static GdUnit4.Assertions;

/// <summary>Traffic and people in the city preview, and what a car does to you.</summary>
[TestSuite]
[RequireGodotRuntime]
public class CityLifeTest
{
    private const string PreviewPath = "res://scenes/stages/city/city_preview.tscn";
    private const string TestPath = "user://test_city_life.tres";

    private ISceneRunner? _runner;

    [Before]
    public void Setup()
    {
        CityStore.Path = TestPath;
        DirAccess.RemoveAbsolute(TestPath);
    }

    [After]
    public void Cleanup()
    {
        DirAccess.RemoveAbsolute(TestPath);
        CityStore.Path = CityStore.SavePath;
    }

    [AfterTest]
    public void CleanupScene()
    {
        _runner?.Dispose();
        _runner = null;
    }

    [TestCase]
    public void TheDriverMixHasCalmAndCrazyDrivers()
    {
        var traffic = GD.Load<TrafficSettings>("res://content/city/traffic.tres");
        var ids = new System.Collections.Generic.List<string>();
        foreach (var driver in traffic.Drivers)
        {
            ids.Add(driver.Id);
            AssertFloat(driver.Weight).IsGreater(0);
        }

        AssertThat(ids).Contains("calm", "normal", "in_a_hurry", "crazy");
    }

    [TestCase]
    public async Task ThePreviewFillsWithTrafficAndPeople()
    {
        var scene = await LoadPreview();
        var runner = _runner!;
        await runner.StepSeconds(1);
        var life = scene.GetNode<CitySimulation>("Stage/Life");
        var sim = life.Sim!;
        AssertInt(sim.Cars.Count).IsGreater(10);
        AssertInt(sim.Walkers.Count).IsGreater(15);
        AssertInt(scene.GetNode<TrafficSpawner>("Stage/Life/Traffic").ShownCount).IsEqual(sim.Cars.Count);
        AssertInt(scene.GetNode<CrowdSpawner>("Stage/Life/Crowd").ShownCount).IsEqual(sim.Walkers.Count);

        // Around the player, who starts outside home.
        var player = scene.GetNode<Node3D>("Player");
        foreach (var car in sim.Cars)
        {
            AssertFloat(car.Position.DistanceTo(ReusableGameSystems.CityGen.CityLayout.ToPlan(player.GlobalPosition))).IsLessEqual(sim.Traffic.DespawnRadius);
        }
    }

    [TestCase]
    public async Task AHitBruisesAndShovesYou()
    {
        var scene = await LoadPreview();
        var runner = _runner!;
        await runner.StepSeconds(0.5);
        var player = scene.GetNode<CharacterMotor>("Player");
        var effects = scene.GetNode<StatusEffectHolder>("Player/StatusEffects").Effects;
        var reactions = scene.GetNode<TrafficReactions>("TrafficReactions");
        float before = player.GlobalPosition.X;

        reactions.HitPlayer(6f, Vector2.Right);
        await runner.Step(10);
        AssertBool(effects.Has("bruised")).IsTrue();
        AssertBool(effects.Has("limping")).IsFalse();
        AssertFloat(player.GlobalPosition.X - before).IsGreater(0.2f);

        // A hard hit leaves you limping too.
        reactions.HitPlayer(12f, Vector2.Right);
        await runner.Step(2);
        AssertBool(effects.Has("limping")).IsTrue();
    }

    private async Task<Node> LoadPreview()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        var scene = GD.Load<PackedScene>(PreviewPath).Instantiate();
        _runner = ISceneRunner.Load(scene, autoFree: true);
        await _runner.Step(5);
        return scene;
    }
}
