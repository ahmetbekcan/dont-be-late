namespace DontBeLate.Tests.City;

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DontBeLate.City;
using GdUnit4;
using Godot;
using ReusableGameSystems.Appearance;
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

    /// <summary>
    /// Minutes of the game's traffic in the game's city: no car waits for good, and no junction stays claimed by a car
    /// that has left it. Short lanes between junctions used to leak claims, freezing a queue and the people waiting to
    /// cross in front of it.
    /// </summary>
    [TestCase]
    public void TrafficNeverJams()
    {
        var style = GD.Load<ReusableGameSystems.CityGen.CityStyle>("res://content/city/city_style.tres");
        var traffic = GD.Load<TrafficSettings>("res://content/city/traffic.tres");
        var crowd = GD.Load<CrowdSettings>("res://content/city/crowd.tres");
        const double dt = 1.0 / 30;
        for (ulong seed = 1; seed <= 3; seed++)
        {
            var layout = ReusableGameSystems.CityGen.CityGenerator.Generate(style, seed);
            var sim = new CityLifeSim(layout, traffic, crowd, seed) { MaxCars = 30 };
            sim.Populate(null);
            var still = new System.Collections.Generic.Dictionary<CarState, double>();
            for (int i = 0; i < 30 * 240; i++)
            {
                sim.Step(dt, i * dt, null);
                foreach (var car in sim.Cars)
                {
                    still[car] = car.Speed < 0.05f ? still.GetValueOrDefault(car) + dt : 0;
                    AssertFloat((float)still[car]).OverrideFailureMessage($"car {car.Id} stuck on lane {car.Lane} in city {seed}").IsLess(90f);
                }

                if (i % 30 == 0)
                {
                    foreach (var lane in layout.Lanes.Lanes)
                    {
                        if (sim.IsClaimed(lane.Id))
                        {
                            AssertBool(sim.Cars.Any(c => c.Claimed == lane.Id)).OverrideFailureMessage($"junction path {lane.Id} claimed by nobody in city {seed}").IsTrue();
                        }
                    }
                }
            }
        }
    }

    [TestCase]
    public void TheCrowdHasKindsOfPeopleWithThingsToDo()
    {
        var crowd = GD.Load<CrowdSettings>("res://content/city/crowd.tres");
        var activities = new System.Collections.Generic.List<string>();
        foreach (var activity in crowd.Activities)
        {
            activities.Add(activity.Id);
            AssertFloat(activity.Weight).IsGreater(0);
        }

        AssertThat(activities).Contains("check_phone", "chat", "window_shop");

        var kinds = new System.Collections.Generic.List<string>();
        foreach (var profile in crowd.Profiles)
        {
            kinds.Add(profile.Id);
            AssertFloat(profile.Weight).IsGreater(0);
            foreach (string id in profile.Activities)
            {
                AssertThat(activities).OverrideFailureMessage($"{profile.Id} does unknown activity {id}").Contains(id);
            }
        }

        AssertThat(kinds).Contains("commuter", "stroller", "local", "phone_zombie");

        // Every gesture the walkers use, the walker scene can show.
        var walker = GD.Load<PackedScene>("res://scenes/characters/walker.tscn").Instantiate();
        var gestures = walker.GetNode<CharacterGestures>("Body/Gestures");
        var known = new System.Collections.Generic.List<string>();
        foreach (var gesture in gestures.Gestures)
        {
            known.Add(gesture.Id);
        }

        walker.Free();
        foreach (var activity in crowd.Activities)
        {
            AssertThat(known).Contains(activity.Gesture);
        }

        foreach (var profile in crowd.Profiles)
        {
            if (profile.WalkingGesture.Length > 0)
            {
                AssertThat(known).Contains(profile.WalkingGesture);
            }
        }

        // Window shopping happens at shops (in the pool, or landmarks like the mall), which the city has.
        var uses = GD.Load<ReusableGameSystems.Pools.ContentPool>("res://content/city/lot_uses/lot_uses.tres");
        var useIds = new System.Collections.Generic.List<string>();
        foreach (var use in uses.Entries)
        {
            useIds.Add(use.Id);
        }

        foreach (var landmark in GD.Load<ReusableGameSystems.CityGen.CityStyle>("res://content/city/city_style.tres").Landmarks)
        {
            if (landmark.Use != null)
            {
                useIds.Add(landmark.Use.Id);
            }
        }

        foreach (string use in GD.Load<WalkerActivityDefinition>("res://content/city/activities/window_shop.tres").LotUses)
        {
            AssertThat(useIds).Contains(use);
        }
    }

    [TestCase]
    public async Task PeopleInThePreviewChatAndExplainThemselves()
    {
        var scene = await LoadPreview();
        var runner = _runner!;
        await runner.StepSeconds(0.5);
        var sim = scene.GetNode<CitySimulation>("Stage/Life").Sim!;
        var player = scene.GetNode<Node3D>("Player");
        var registry = new ReusableGameSystems.DebugTools.CommandRegistry();
        DontBeLate.DebugCommands.CityLifeCommands.Register(registry);

        // Nearest to the player: chat with whoever is near (someone is, in a crowd this size, or "can't").
        var result = registry.Execute("crowd do chat");
        AssertBool(result.Success).IsTrue();
        var nearest = sim.NearestWalker(ReusableGameSystems.CityGen.CityLayout.ToPlan(player.GlobalPosition))!;
        if (nearest.Partner != null)
        {
            await runner.Step(5);
            AssertString(registry.Execute("crowd why").Output).Contains("chat");
            bool talking = false;
            foreach (var child in scene.GetNode<CrowdSpawner>("Stage/Life/Crowd").GetChildren())
            {
                if (child is Node3D { Visible: true } shown
                    && ReusableGameSystems.CityGen.CityLayout.ToPlan(shown.GlobalPosition).DistanceTo(nearest.Position) < 0.01f)
                {
                    talking = shown.GetNode<CharacterGestures>("Body/Gestures").Gesture == "talk";
                }
            }

            AssertBool(talking).IsTrue();
        }

        AssertString(registry.Execute("crowd kinds").Output).Contains("Activities: check_phone, chat, window_shop");

        // Labels over everyone near: on, then off.
        AssertString(registry.Execute("crowd labels").Output).StartsWith("labels on");
        var labels = scene.GetNode<CrowdLabels>("Stage/Life/CrowdLabels");
        await runner.Step(15);
        AssertInt(labels.ShownCount).IsGreater(0);
        AssertString(registry.Execute("crowd labels off").Output).IsEqual("labels off");
        await runner.Step(2);
        AssertInt(labels.ShownCount).IsEqual(0);
        AssertBool(registry.Execute("crowd do juggle").Success).IsFalse();
    }

    [TestCase]
    public async Task ThePreviewFillsWithTrafficAndPeople()
    {
        var scene = await LoadPreview();
        var runner = _runner!;
        await runner.StepSeconds(1);
        var life = scene.GetNode<CitySimulation>("Stage/Life");
        var sim = life.Sim!;
        // Anonymous traffic fills up to its count; the town's own drivers come on top.
        AssertInt(sim.Cars.Count(c => c.Owner == null)).IsEqual(sim.MaxCars);
        // Passers-by fill up to the crowd's count; the town's own people (citizens) come on top.
        int passersBy = 0;
        foreach (var walker in sim.Walkers)
        {
            passersBy += walker.Owner == null ? 1 : 0;
        }

        AssertInt(passersBy).IsGreaterEqual(sim.MaxWalkers - 2);
        AssertInt(scene.GetNode<TrafficSpawner>("Stage/Life/Traffic").ShownCount).IsEqual(sim.Cars.Count);
        AssertInt(scene.GetNode<CrowdSpawner>("Stage/Life/Crowd").ShownCount).IsEqual(sim.Walkers.Count);

        // Around the player, who starts outside home.
        var player = scene.GetNode<Node3D>("Player");
        foreach (var car in sim.Cars)
        {
            AssertFloat(car.Position.DistanceTo(ReusableGameSystems.CityGen.CityLayout.ToPlan(player.GlobalPosition))).IsLessEqual(sim.Traffic.DespawnRadius);
        }

        // A bang near them: the people around are startled, and it shows on their faces.
        var crowd = scene.GetNode<CrowdSpawner>("Stage/Life/Crowd");
        AssertObject(crowd.Feelings[ReusableGameSystems.CityLife.WalkerFeelings.Annoyed]).IsNotNull();
        sim.DrawAttention(ReusableGameSystems.CityGen.CityLayout.ToPlan(player.GlobalPosition), 1000, 1);
        await runner.Step(1);
        int surprised = 0;
        foreach (var child in crowd.GetChildren())
        {
            if (child is Node3D { Visible: true } shown && shown.GetNodeOrNull<CharacterFace>("Body/Face") is { } face)
            {
                surprised += face.Showing?.Id == "surprised" ? 1 : 0;
            }
        }

        AssertInt(surprised).IsGreater(0);
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
