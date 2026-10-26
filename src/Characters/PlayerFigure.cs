using Godot;
using ReusableGameSystems.Appearance;

namespace DontBeLate.Characters;

/// <summary>Puts the player's look (<see cref="PlayerLookStore"/>) on its parent <see cref="CharacterBody"/> and keeps it current.</summary>
[GlobalClass]
public partial class PlayerFigure : Node
{
    public override void _Ready()
    {
        Apply(PlayerLookStore.Load());
        PlayerLookStore.Changed += Apply;
    }

    public override void _ExitTree() => PlayerLookStore.Changed -= Apply;

    private void Apply(CharacterLook look)
    {
        if (GetParent() is CharacterBody body)
        {
            body.Look = look;
        }
    }
}
