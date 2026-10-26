namespace DontBeLate.Tests.City;

using System.Threading.Tasks;
using DontBeLate.City;
using DontBeLate.PhoneApps;
using GdUnit4;
using Godot;
using ReusableGameSystems.CityGen;
using ReusableGameSystems.Characters;
using ReusableGameSystems.Items;
using ReusableGameSystems.Phone;
using ReusableGameSystems.Randomness;
using ReusableGameSystems.Runs;
using ReusableGameSystems.Tests;
using static GdUnit4.Assertions;

/// <summary>The game's city: its style, the seed modes, saving the choice, and the preview scene.</summary>
[TestSuite]
[RequireGodotRuntime]
public class CityTest
{
    private const string StylePath = "res://content/city/city_style.tres";
    private const string PreviewPath = "res://scenes/stages/city/city_preview.tscn";
    private const string TestPath = "user://test_city.tres";

    private static readonly string[] Landmarks = { "home", "bus_stop", "photo_studio", "bank" };

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
        CityStore.Path = TestPath;
        DirAccess.RemoveAbsolute(TestPath);
    }

    [TestCase]
    public void EveryCityHasEveryLandmark()
    {
        var style = GD.Load<CityStyle>(StylePath);
        for (ulong seed = 1; seed <= 50; seed++)
        {
            var city = CityGenerator.Generate(style, seed);
            foreach (string id in Landmarks)
            {
                var landmark = city.FindLandmark(id);
                AssertObject(landmark).OverrideFailureMessage($"no {id} in city {seed}").IsNotNull();
                AssertInt(landmark!.WalkNode).IsGreaterEqual(0);
            }

            // Home and the bus stop are a walk apart, so missing the bus means something.
            float walk = city.FindLandmark("home")!.Position.DistanceTo(city.FindLandmark("bus_stop")!.Position);
            AssertFloat(walk).OverrideFailureMessage($"home and bus stop {walk:0} m apart in city {seed}").IsGreater(40f);
        }
    }

    [TestCase]
    public void NewEachRunFollowsTheRunSeedAndKeptIgnoresIt()
    {
        var settings = new CitySettings { Mode = CitySeedMode.NewEachRun };
        AssertThat(settings.SeedFor(100)).IsEqual(settings.SeedFor(100));
        AssertThat(settings.SeedFor(100)).IsNotEqual(settings.SeedFor(101));

        settings.Mode = CitySeedMode.Kept;
        settings.KeptSeed = Seeds.ToCode(777);
        AssertThat(settings.SeedFor(100)).IsEqual(777UL);
        AssertThat(settings.SeedFor(5)).IsEqual(777UL);

        // Kept with nothing kept yet falls back to the run's city.
        settings.KeptSeed = "";
        AssertThat(settings.SeedFor(100)).IsEqual(new CitySettings().SeedFor(100));
    }

    [TestCase]
    public void TheChoiceIsSavedAndLoaded()
    {
        AssertObject(CityStore.Load().Mode).IsEqual(CitySeedMode.NewEachRun);
        AssertObject(CityStore.Save(new CitySettings { Mode = CitySeedMode.Kept, KeptSeed = "00000000000000AB" })).IsEqual(Error.Ok);
        var loaded = CityStore.Load();
        AssertObject(loaded.Mode).IsEqual(CitySeedMode.Kept);
        AssertString(loaded.KeptSeed).IsEqual("00000000000000AB");
    }

    [TestCase]
    public async Task ThePreviewPutsYouOutsideHomeOnTheGround()
    {
        var scene = await LoadPreview();
        var stage = scene.GetNode<CityStage>("Stage");
        var player = scene.GetNode<CharacterMotor>("Player");
        AssertObject(stage.Layout).IsNotNull();
        await _runner!.StepSeconds(1);

        var home = stage.Layout!.FindLandmark("home")!;
        AssertFloat(CityLayout.ToPlan(player.GlobalPosition).DistanceTo(home.Position)).IsLess(3f);
        AssertFloat(player.GlobalPosition.Y).IsGreater(stage.Layout.GroundHeight - 0.05f);
        AssertBool(player.IsOnFloor()).IsTrue();
    }

    [TestCase]
    public async Task KeepingTheCityBringsItBackNextRun()
    {
        var scene = await LoadPreview();
        var stage = scene.GetNode<CityStage>("Stage");
        string first = stage.SeedCode;

        var runner = _runner!;
        stage.KeepCurrent();
        RunManager.Instance!.StartRun();
        await runner.Step(2);
        AssertString(stage.SeedCode).IsEqual(first);

        stage.SetMode(CitySeedMode.NewEachRun);
        RunManager.Instance.StartRun();
        await runner.Step(2);
        AssertString(stage.SeedCode).IsNotEqual(first);
    }

    [TestCase]
    public async Task GotoMovesThePlayerToALandmark()
    {
        var scene = await LoadPreview();
        var stage = scene.GetNode<CityStage>("Stage");
        AssertBool(stage.MovePlayerTo("bank")).IsTrue();
        await _runner!.Step(5);
        var bank = stage.Layout!.FindLandmark("bank")!;
        AssertFloat(CityLayout.ToPlan(scene.GetNode<Node3D>("Player").GlobalPosition).DistanceTo(bank.Position)).IsLess(3f);
        AssertBool(stage.MovePlayerTo("moon")).IsFalse();
    }

    [TestCase]
    public async Task TheMapShowsThePlacesAndTheWayThere()
    {
        var scene = await LoadPreview();
        var phone = scene.GetNode<PhoneScreen>("Phone");
        var runner = _runner!;
        scene.GetNode<HotbarHolder>("Player/Hotbar").Select(0);
        await runner.Step(10);
        AssertBool(phone.OpenApp("map")).IsTrue();
        await runner.Step(5);

        var view = phone.CurrentView as MapAppView;
        AssertObject(view).IsNotNull();
        AssertObject(view!.Map).IsNotNull();
        AssertInt(view.GetNode("Places").GetChildCount()).IsEqual(Landmarks.Length);

        view.Select("bank");
        await runner.Step(2);
        AssertFloat(view.RouteLength).IsGreater(10f);
        AssertString(view.PlaceText).StartsWith("Bank\n");
        AssertString(view.PlaceText).Contains("min walk");
        AssertObject(view.Map!.Route).IsNotNull();

        view.Select("home");
        await runner.Step(2);
        AssertString(view.PlaceText).IsEqual("Home\nYou're here");
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
