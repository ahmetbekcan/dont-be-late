namespace DontBeLate.Tests.Stages;

using ReusableGameSystems.Tests;
using System.Linq;
using System.Threading.Tasks;
using DontBeLate.Obstacles;
using DontBeLate.Scenarios;
using DontBeLate.Stages;
using GdUnit4;
using Godot;
using ReusableGameSystems.Characters;
using ReusableGameSystems.Clock;
using ReusableGameSystems.Interaction;
using ReusableGameSystems.Items;
using ReusableGameSystems.Meters;
using ReusableGameSystems.Runs;
using ReusableGameSystems.StatusEffects;
using ReusableGameSystems.UI;
using ReusableGameSystems.WorldState;
using static GdUnit4.Assertions;

/// <summary>
/// Plays the real morning scene. Nothing is scripted per scenario: each test puts the player somewhere and lets
/// the world react (rain, puddles, the cat, the bus driver), the way a player would experience it.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class MorningTest
{
    private const string ScenePath = "res://scenes/main/morning.tscn";
    private const uint FrameMs = 16;

    // Places in the world (the flat's floor is at y = 3).
    private static readonly Vector3 InTheRain = new(24, 0.05f, -4.6f);
    private static readonly Vector3 AtTheBusStop = new(38, 0.05f, -6.4f);
    private static readonly Vector3 InAPuddle = new(9.5f, 0.05f, -5.4f);

    private ISceneRunner _runner = null!;
    private Node _scene = null!;

    private RunLoop Loop => RunManager.Instance!.Loop;

    private MorningDirector Director => _scene.GetNode<MorningDirector>("Director");

    private Blackboard Facts => _scene.GetNode<WorldStateStore>("World").Facts;

    private EndScreen EndScreen => _scene.GetNode<EndScreen>("EndScreen");

    private CharacterMotor Player => _scene.GetNode<CharacterMotor>("Player");

    private Meter Wetness => _scene.GetNode<MeterHolder>("Player/Meters").Get("wetness")!;

    private StatusEffectSet Status => _scene.GetNode<StatusEffectHolder>("Player/StatusEffects").Effects;

    private Inventory Inventory => _scene.GetNode<InventoryHolder>("Player/Inventory").Inventory;

    private Cat Cat => _scene.GetNode<Cat>("Home/Flat/Props/Cat");

    private BusService Buses => _scene.GetNode<BusService>("Street/Buses");

    [AfterTest]
    public void Cleanup()
    {
        ScenarioQueue.Clear();
        _runner?.Dispose();
        if (Engine.GetMainLoop() is SceneTree tree)
        {
            tree.Paused = false;
        }
    }

    /// <summary>Loads the morning with exactly the given scenarios (no random extras, no normal-morning roll).</summary>
    private async Task LoadMorning(params string[] scenarioIds)
    {
        // A real scene change removes the old scene before adding the new one; wait until any previous test scene is gone.
        var tree = (SceneTree)Engine.GetMainLoop();
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);

        ScenarioQueue.Clear();
        foreach (string id in scenarioIds)
        {
            ScenarioQueue.Force(id);
        }

        _scene = GD.Load<PackedScene>(ScenePath).Instantiate();
        var director = _scene.GetNode<MorningDirector>("Director");
        director.ScenarioCountsByRun = new[] { 0 };
        director.NormalMorningChance = 0;
        _runner = ISceneRunner.Load(_scene, autoFree: true);
        await _runner.Step(5);
    }

    private Interactable Thing(string path) => _scene.GetNode<Interactable>($"Home/Flat/Props/{path}/Interactable");

    private bool Use(string path, string verbId) =>
        Thing(path).Interact(verbId, Player, _scene.GetNode<Interactor>("Player/Head/Interactor").Context);

    private bool CanUse(string path, string verbId)
    {
        var interactable = Thing(path);
        return interactable.IsAvailable(interactable.FindVerb(verbId)!, _scene.GetNode<Interactor>("Player/Head/Interactor").Context);
    }

    private async Task MoveTo(Vector3 position, uint frames = 10)
    {
        Player.GlobalPosition = position;
        Player.Velocity = Vector3.Zero;
        await _runner.Step(frames);
    }

    /// <summary>Steps frames until <paramref name="condition"/> holds (or gives up after <paramref name="maxFrames"/>).</summary>
    private async Task WaitUntil(System.Func<bool> condition, int maxFrames = 180)
    {
        for (int i = 0; i < maxFrames && !condition(); i++)
        {
            await _runner.Step(1);
        }
    }

    /// <summary>Moves a rigid body right away (setting its position alone can be overwritten by the physics step).</summary>
    private static void Teleport(RigidBody3D body, Vector3 position, Vector3 velocity)
    {
        var transform = new Transform3D(Basis.Identity, position);
        PhysicsServer3D.BodySetState(body.GetRid(), PhysicsServer3D.BodyState.Transform, transform);
        PhysicsServer3D.BodySetState(body.GetRid(), PhysicsServer3D.BodyState.LinearVelocity, velocity);
        PhysicsServer3D.BodySetState(body.GetRid(), PhysicsServer3D.BodyState.Sleeping, false);
        body.GlobalTransform = transform;
        body.LinearVelocity = velocity;
    }

    private async Task SetTime(string time, uint frames = 3)
    {
        GameClock.Instance!.SetTimeText(time);
        await _runner.Step(frames);
    }

    [TestCase]
    public async Task MorningStartsAtSevenThirtyWithTheForcedScenario()
    {
        await LoadMorning("rain");

        AssertBool(Loop.IsRunning).IsTrue();
        AssertString(GameClock.Instance!.Now.ToString()).IsEqual("07:30");
        AssertArray(Director.ActiveScenarios.Select(s => s.Id).ToList()).ContainsExactly("rain");
        AssertBool(Facts.GetBool("raining")).IsTrue();
        AssertBool(Facts.GetBool("indoors")).IsTrue();
    }

    [TestCase]
    public async Task CatchingABusEndsTheMorningOnTime()
    {
        await LoadMorning();
        Use("Sneakers", "wear");
        await MoveTo(AtTheBusStop);
        await SetTime("08:00");

        AssertBool(Buses.TryBoard()).IsTrue();

        AssertThat(Loop.LastOutcome).IsEqual(RunOutcome.Completed);
        AssertString(Loop.LastReason).Contains("8:01").Contains("08:50");
        AssertString(EndScreen.TitleText).IsEqual("On the bus!");
        AssertString(EndScreen.MessageText).Contains("in sneakers");
    }

    [TestCase]
    public async Task NoBusAtTheStopMeansNoBoarding()
    {
        await LoadMorning();
        Use("Sneakers", "wear");
        await SetTime("07:50");

        AssertBool(Buses.TryBoard()).IsFalse();
        AssertBool(Loop.IsRunning).IsTrue();
    }

    [TestCase]
    public async Task MissingTheLastOnTimeBusFailsTheMorning()
    {
        await LoadMorning();
        await SetTime("08:05");
        AssertBool(Loop.IsRunning).IsTrue();

        await SetTime("08:12");

        AssertThat(Loop.LastOutcome).IsEqual(RunOutcome.Failed);
        AssertString(Loop.LastReason).Contains("8:11");
        AssertString(EndScreen.TitleText).IsEqual("Not today.");
    }

    [TestCase]
    public async Task NothingStopsYouLeavingBarefootButThePavementHurtsAndTheDriverSaysNo()
    {
        await LoadMorning();
        await MoveTo(InTheRain, 45);

        AssertBool(Facts.GetBool("indoors")).IsFalse();
        AssertBool(Status.Has("sore_feet")).IsTrue();
        AssertFloat(Status.Multiplier("move_speed")).IsLess(1f);

        await MoveTo(AtTheBusStop);
        await SetTime("08:00");
        string? refusedBy = null;
        Buses.Refused += (rule, line) => refusedBy = rule;

        AssertBool(Buses.TryBoard()).IsFalse();
        AssertString(refusedBy).IsEqual("barefoot");
        AssertBool(Loop.IsRunning).IsTrue();
    }

    [TestCase]
    public async Task RainSoaksYouUnlessYouHaveTheUmbrella()
    {
        await LoadMorning("rain");
        Use("Sneakers", "wear");
        await MoveTo(InTheRain, 60);
        float soaked = Wetness.Value;
        AssertFloat(soaked).IsGreater(2f);

        Wetness.Set(0);
        Use("Umbrella", "take");
        await _runner.Step(60);
        AssertFloat(Wetness.Value).IsLess(0.5f);
    }

    [TestCase]
    public async Task NoRainOnADryMorning()
    {
        await LoadMorning();
        Use("Sneakers", "wear");
        await MoveTo(InTheRain, 60);

        AssertFloat(Wetness.Value).IsEqual(0f);
    }

    [TestCase]
    public async Task PuddlesSoakSneakersButNotBoots()
    {
        await LoadMorning("rain");
        Use("Umbrella", "take");
        Use("Boots", "wear");
        await MoveTo(InAPuddle, 15);
        AssertBool(Status.Has("wet_socks")).IsFalse();

        await MoveTo(InTheRain, 5);
        Inventory.Unequip("feet");
        Use("Sneakers", "wear");
        await MoveTo(InAPuddle, 15);

        AssertBool(Status.Has("wet_socks")).IsTrue();
        AssertFloat(Wetness.Value).IsGreaterEqual(6f);
    }

    [TestCase]
    public async Task TheDriverRefusesASoakedPassengerUntilTheyDryOff()
    {
        await LoadMorning("rain");
        Use("Sneakers", "wear");
        await MoveTo(AtTheBusStop);
        await SetTime("07:45");
        Wetness.Set(80);

        AssertBool(Buses.TryBoard()).IsFalse();
        AssertBool(Loop.IsRunning).IsTrue();

        // The towel is at home, but the effect doesn't care where you are.
        Use("Towel", "dry");
        AssertBool(Buses.TryBoard()).IsTrue();
        AssertThat(Loop.LastOutcome).IsEqual(RunOutcome.Completed);
    }

    [TestCase]
    public async Task JumpingOutOfTheWindowWorksButYouLimp()
    {
        await LoadMorning();
        _scene.GetNode<Openable>("Home/Flat/Shell/WindowGlass/Openable").SetOpen(true);
        await _runner.Step(2);

        // Just outside the window, at sill height.
        await MoveTo(new Vector3(2, 3.6f, -4.8f), 70);

        AssertFloat(Player.GlobalPosition.Y).IsLess(0.3f);
        AssertBool(Status.Has("limping")).IsTrue();
        AssertFloat(Status.Multiplier("move_speed")).IsLess(0.6f);
    }

    [TestCase]
    public async Task CatOnTheShoesCanBeWrestled()
    {
        await LoadMorning("cat_on_shoes");
        await _runner.Step(5);
        AssertThat(Cat.State).IsEqual(CatState.GuardingShoes);
        AssertBool(CanUse("Sneakers", "wear")).IsFalse();
        AssertBool(CanUse("Sneakers", "grab")).IsTrue();

        int tries = 0;
        while (!Inventory.IsEquipped("sneakers") && tries < 40)
        {
            tries++;
            Use("Sneakers", "grab");
            await _runner.Step(2);
        }

        AssertBool(Inventory.IsEquipped("sneakers")).IsTrue();
        AssertBool(Facts.GetBool("cat_on_shoes")).IsFalse();
        AssertBool(Status.Has("scratched")).IsTrue();
        AssertBool(Cat.State is CatState.Fleeing or CatState.Sleeping).IsTrue();
    }

    [TestCase]
    public async Task TunaOnTheFloorLuresTheCatOffTheShoes()
    {
        await LoadMorning("cat_on_shoes");
        var tuna = _scene.GetNode<RigidBody3D>("Home/Flat/Props/Tuna");
        Teleport(tuna, new Vector3(3.0f, 3.1f, 0.6f), Vector3.Zero);

        await WaitUntil(() => Cat.State != CatState.GuardingShoes);

        AssertBool(Cat.State is CatState.GoingToFood or CatState.Eating).IsTrue();
        AssertBool(Facts.GetBool("cat_on_shoes")).IsFalse();
        AssertBool(CanUse("Sneakers", "wear")).IsTrue();
    }

    [TestCase]
    public async Task TunaInTheClosedFridgeDoesNothing()
    {
        await LoadMorning("cat_on_shoes");

        await _runner.Step(60);

        AssertThat(Cat.State).IsEqual(CatState.GuardingShoes);
    }

    [TestCase]
    public async Task ThrowingSomethingAtTheCatScaresItOff()
    {
        await LoadMorning("cat_on_shoes");
        var book = _scene.GetNode<RigidBody3D>("Home/Flat/Props/Book");
        Teleport(book, Cat.GlobalPosition + new Vector3(-1.2f, 0.25f, 0), new Vector3(8, 0, 0));

        await WaitUntil(() => Cat.State != CatState.GuardingShoes, 60);

        AssertBool(Cat.State is CatState.Fleeing or CatState.Sleeping).IsTrue();
        AssertBool(Facts.GetBool("cat_on_shoes")).IsFalse();
    }

    [TestCase]
    public async Task SlippersAreFineForTheDriverButSlowOutside()
    {
        await LoadMorning();
        Use("Slippers", "wear");
        await MoveTo(AtTheBusStop, 45);
        AssertBool(Status.Has("slipper_flap")).IsTrue();

        await SetTime("08:00");
        AssertBool(Buses.TryBoard()).IsTrue();
        AssertString(EndScreen.MessageText).Contains("slippers");
    }
}
