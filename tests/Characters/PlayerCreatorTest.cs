namespace DontBeLate.Tests.Characters;

using System.Threading.Tasks;
using DontBeLate.Characters;
using GdUnit4;
using Godot;
using ReusableGameSystems.Appearance;
using ReusableGameSystems.Tests;
using ReusableGameSystems.UI;
using static GdUnit4.Assertions;

/// <summary>"Change look" in the inventory: opens the creator, saves, and comes back to the inventory.</summary>
[TestSuite]
[RequireGodotRuntime]
public class PlayerCreatorTest
{
    private const string TestPath = "user://test_player_creator_look.tres";

    [Before]
    public void Setup()
    {
        PlayerLookStore.Path = TestPath;
        DirAccess.RemoveAbsolute(TestPath);
    }

    [After]
    public void Cleanup()
    {
        DirAccess.RemoveAbsolute(TestPath);
        PlayerLookStore.Path = PlayerLookStore.SavePath;
    }

    [TestCase]
    public async Task ChangeLookButtonOpensTheCreatorAndReturns()
    {
        var root = new Node { Name = "Root" };
        var inventory = new InventoryScreen { Name = "Inventory" };
        var creator = new CharacterCreatorScreen { Name = "Creator", Catalog = ResourceLoader.Load<CharacterCatalog>("res://content/characters/kit/catalog.tres") };
        var player = new PlayerCreator { Creator = creator, Inventory = inventory };
        root.AddChild(inventory);
        root.AddChild(creator);
        root.AddChild(player);
        using var runner = ISceneRunner.Load(root, autoFree: true);

        var button = (Button)inventory.FigureBar.GetChild(0);
        AssertString(button.Text).IsEqual("Change look");

        inventory.Open();
        await runner.Step(1);
        button.EmitSignal(BaseButton.SignalName.Pressed);
        await runner.Step(1);
        AssertBool(inventory.IsOpen).IsFalse();
        AssertBool(creator.IsOpen).IsTrue();

        // Done saves the look and goes back to the inventory.
        creator.Finish();
        await runner.Step(2);
        AssertBool(creator.IsOpen).IsFalse();
        AssertBool(inventory.IsOpen).IsTrue();
        AssertBool(ResourceLoader.Exists(TestPath)).IsTrue();

        // Opened some other way (the console), it doesn't open the inventory afterwards.
        inventory.Close();
        player.Open();
        creator.Cancel();
        await runner.Step(2);
        AssertBool(inventory.IsOpen).IsFalse();
    }
}
