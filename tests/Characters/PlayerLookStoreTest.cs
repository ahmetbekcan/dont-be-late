namespace DontBeLate.Tests.Characters;

using DontBeLate.Characters;
using GdUnit4;
using Godot;
using ReusableGameSystems.Appearance;
using static GdUnit4.Assertions;

/// <summary>The player's look: default until saved, then the saved one.</summary>
[TestSuite]
[RequireGodotRuntime]
public class PlayerLookStoreTest
{
    private const string TestPath = "user://test_player_look.tres";

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
    public void LoadsTheDefaultUntilSavedThenTheSavedLook()
    {
        var look = PlayerLookStore.Load();
        AssertObject(look.GetPart(CharacterSlots.Head)).IsNotNull();

        CharacterLook? changed = null;
        void OnChanged(CharacterLook l) => changed = l;
        PlayerLookStore.Changed += OnChanged;
        look.SkinColor = new Color(1, 0.86f, 0.25f);
        look.Parts.Remove(CharacterSlots.Hair);
        AssertObject(PlayerLookStore.Save(look)).IsEqual(Error.Ok);
        PlayerLookStore.Changed -= OnChanged;
        AssertObject(changed).IsSame(look);

        var loaded = PlayerLookStore.Load();
        AssertObject(loaded.SkinColor).IsEqual(new Color(1, 0.86f, 0.25f));
        AssertObject(loaded.GetPart(CharacterSlots.Hair)).IsNull();
        AssertString(loaded.GetPart(CharacterSlots.Head)!.ResourcePath).StartsWith("res://content/characters/kit/");
    }
}
