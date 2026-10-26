using Godot;
using ReusableGameSystems.Appearance;

namespace DontBeLate.Characters;

/// <summary>Opens the character creator on the player's look and saves the result (<see cref="PlayerLookStore"/>).</summary>
[GlobalClass]
public partial class PlayerCreator : Node
{
    [Export]
    public CharacterCreatorScreen? Creator { get; set; }

    public static PlayerCreator? Current { get; private set; }

    public override void _EnterTree() => Current = this;

    public override void _Ready()
    {
        if (Creator != null)
        {
            Creator.Finished += OnFinished;
        }
    }

    public override void _ExitTree()
    {
        if (Current == this)
        {
            Current = null;
        }

        if (Creator != null)
        {
            Creator.Finished -= OnFinished;
        }
    }

    public void Open() => Creator?.Open(PlayerLookStore.Load());

    private static void OnFinished(CharacterLook look) => PlayerLookStore.Save(look);
}
