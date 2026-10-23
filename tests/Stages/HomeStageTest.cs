namespace DontBeLate.Tests.Stages;

using System.Linq;
using System.Threading.Tasks;
using DontBeLate.Scenarios;
using DontBeLate.Stages;
using GdUnit4;
using Godot;
using ReusableGameSystems.Clock;
using ReusableGameSystems.Interaction;
using ReusableGameSystems.Runs;
using ReusableGameSystems.UI;
using ReusableGameSystems.WorldState;
using static GdUnit4.Assertions;

/// <summary>
/// Plays the real Home scene end to end: each test forces the morning's scenarios, uses objects the way the player
/// would (through their interaction verbs), and checks how the run ends.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class HomeStageTest
{
    private const string ScenePath = "res://scenes/stages/home/home.tscn";

    private ISceneRunner _runner = null!;
    private Node _scene = null!;

    private RunLoop Loop => RunManager.Instance!.Loop;

    private MorningDirector Director => _scene.GetNode<MorningDirector>("Director");

    private Blackboard Facts => _scene.GetNode<WorldStateStore>("World").Facts;

    private EndScreen EndScreen => _scene.GetNode<EndScreen>("EndScreen");

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

    /// <summary>Loads Home with exactly the given scenarios (no random extras, no normal-morning roll).</summary>
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
        await _runner.SimulateFrames(5);
    }

    private bool Use(string objectName, string verbId)
    {
        var interactable = _scene.GetNode<Interactable>($"Props/{objectName}/Interactable");
        var context = _scene.GetNode<Interactor>("Player/Head/Interactor").Context;
        return interactable.Interact(verbId, _scene.GetNode("Player"), context);
    }

    private bool CanUse(string objectName, string verbId)
    {
        var interactable = _scene.GetNode<Interactable>($"Props/{objectName}/Interactable");
        var verb = interactable.FindVerb(verbId)!;
        return interactable.IsAvailable(verb, _scene.GetNode<Interactor>("Player/Head/Interactor").Context);
    }

    [TestCase]
    public async Task MorningStartsAtSevenThirtyWithForcedScenario()
    {
        await LoadMorning("rain");

        AssertBool(Loop.IsRunning).IsTrue();
        AssertString(GameClock.Instance!.Now.ToString()).IsEqual("07:30");
        AssertArray(Director.ActiveScenarios.Select(s => s.Id).ToList()).ContainsExactly("rain");
        AssertBool(Facts.GetBool("raining")).IsTrue();
    }

    [TestCase]
    public async Task RainWithUmbrellaAndBootsGetsOutTheDoor()
    {
        await LoadMorning("rain");

        AssertBool(Use("Umbrella", "take")).IsTrue();
        AssertBool(Use("Boots", "wear")).IsTrue();
        AssertBool(Use("FrontDoor", "leave")).IsTrue();

        AssertThat(Loop.LastOutcome).IsEqual(RunOutcome.Completed);
        AssertBool(EndScreen.IsShown).IsTrue();
        AssertString(EndScreen.TitleText).IsEqual("Out the door!");
    }

    [TestCase]
    public async Task RainInSneakersGetsYouSoaked()
    {
        await LoadMorning("rain");

        Use("Umbrella", "take");
        Use("Sneakers", "wear");
        Use("FrontDoor", "leave");

        AssertThat(Loop.LastOutcome).IsEqual(RunOutcome.Failed);
        AssertString(Loop.LastReason).Contains("soaked");
        AssertString(EndScreen.TitleText).IsEqual("Not today.");
    }

    [TestCase]
    public async Task CatBlocksTheShoesUntilBribedWithTuna()
    {
        await LoadMorning("cat_on_shoes");

        AssertBool(CanUse("Boots", "wear")).IsFalse();
        AssertBool(CanUse("CatOnShoes", "bribe")).IsFalse();

        AssertBool(Use("Fridge", "take_tuna")).IsTrue();
        AssertBool(Use("Fridge", "take_tuna")).OverrideFailureMessage("the fridge has one can").IsFalse();
        AssertBool(Use("CatOnShoes", "bribe")).IsTrue();
        await _runner.SimulateFrames(20);

        AssertBool(Facts.GetBool("cat_on_shoes")).IsFalse();
        AssertBool(_scene.GetNode<Node3D>("Props/CatOnShoes").Visible).IsFalse();
        AssertBool(_scene.GetNode<Node3D>("Props/SleepingCat").Visible).OverrideFailureMessage("the cat goes back to its bed").IsTrue();
        AssertBool(Use("Boots", "wear")).IsTrue();
        AssertBool(Use("FrontDoor", "leave")).IsTrue();
        AssertThat(Loop.LastOutcome).IsEqual(RunOutcome.Completed);
    }

    [TestCase]
    public async Task FightingTheCatCostsFiveMinutes()
    {
        await LoadMorning("cat_on_shoes");
        var before = GameClock.Instance!.Now;

        AssertBool(Use("CatOnShoes", "fight")).IsTrue();

        AssertThat(GameClock.Instance.Now.TotalMinutes - before.TotalMinutes).IsEqual(5.0);
        AssertBool(Facts.GetBool("scratched")).IsTrue();
        AssertBool(CanUse("Sneakers", "wear")).IsTrue();
    }

    [TestCase]
    public async Task SlippersAreFineOnADryDay()
    {
        await LoadMorning("cat_on_shoes");

        AssertBool(Use("Slippers", "wear")).IsTrue();
        AssertBool(Use("FrontDoor", "leave")).IsTrue();

        AssertThat(Loop.LastOutcome).IsEqual(RunOutcome.Completed);
    }

    [TestCase]
    public async Task CannotLeaveBarefoot()
    {
        await LoadMorning();

        AssertInt(Director.ActiveScenarios.Count).IsEqual(0);
        AssertBool(Use("FrontDoor", "leave")).IsFalse();
        AssertBool(Loop.IsRunning).IsTrue();
    }

    [TestCase]
    public async Task MissingTheBusFailsTheRun()
    {
        await LoadMorning();

        GameClock.Instance!.Keeper.SetTime(ClockTime.Parse("8:00"));

        AssertThat(Loop.LastOutcome).IsEqual(RunOutcome.Failed);
        AssertString(Loop.LastReason).Contains("missed the 8:00 bus");
        AssertBool(GameClock.Instance.Paused).IsTrue();
    }

    [TestCase]
    public async Task CluesFollowTheScenarios()
    {
        await LoadMorning("rain", "cat_on_shoes");
        await _runner.SimulateFrames(5);
        AssertBool(_scene.GetNode<Node3D>("Outside/RainClues").Visible).IsTrue();
        AssertBool(_scene.GetNode<Node3D>("Props/CatOnShoes").Visible).IsTrue();
        AssertBool(_scene.GetNode<Node3D>("Props/SleepingCat").Visible).IsFalse();

        _runner.Dispose();
        await LoadMorning();
        await _runner.SimulateFrames(5);
        AssertBool(_scene.GetNode<Node3D>("Outside/RainClues").Visible).IsFalse();
        AssertBool(_scene.GetNode<Node3D>("Props/CatOnShoes").Visible).IsFalse();
        AssertBool(_scene.GetNode<Node3D>("Props/SleepingCat").Visible).IsTrue();
    }

    [TestCase]
    public async Task DebugCommandsForceAndListScenarios()
    {
        await LoadMorning("rain");
        var console = ReusableGameSystems.DebugTools.DebugConsole.Instance!;

        AssertString(console.Commands.Execute("scenario").Output).Contains("rain: Rain overnight");
        AssertString(console.Commands.Execute("scenario list").Output).Contains("cat_on_shoes");
        AssertBool(console.Commands.Execute("scenario force nope").Success).IsFalse();
        AssertString(console.Commands.Execute("scenario force cat_on_shoes").Output).Contains("forced for next morning: cat_on_shoes");
        AssertArray(ScenarioQueue.Pending.ToList()).ContainsExactly("cat_on_shoes");
    }
}
