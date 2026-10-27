using Godot;
using ReusableGameSystems.Appearance;
using ReusableGameSystems.UI;

namespace DontBeLate.Characters;

/// <summary>
/// Opens the character creator on the player's look and saves the result (<see cref="PlayerLookStore"/>).
/// With an <see cref="Inventory"/> screen it adds a button under the figure there, and goes back to the
/// inventory when the creator closes.
/// </summary>
[GlobalClass]
public partial class PlayerCreator : Node
{
    private bool _fromInventory;

    [Export]
    public CharacterCreatorScreen? Creator { get; set; }

    /// <summary>The inventory screen that gets the button. Null = no button.</summary>
    [Export]
    public InventoryScreen? Inventory { get; set; }

    [Export]
    public string ButtonText { get; set; } = "Change look";

    public static PlayerCreator? Current { get; private set; }

    public override void _EnterTree() => Current = this;

    public override void _Ready()
    {
        if (Creator != null)
        {
            Creator.Finished += OnFinished;
            Creator.Cancelled += OnCancelled;
        }

        Inventory?.AddFigureButton(ButtonText, OpenFromInventory, closeFirst: true);

        // Started with "-- --creator" (play_game.bat looks): straight into the creator to try clothes.
        if (System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--creator") >= 0)
        {
            CallDeferred(MethodName.Open);
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
            Creator.Cancelled -= OnCancelled;
        }
    }

    public void Open()
    {
        _fromInventory = false;
        Creator?.Open(PlayerLookStore.Load());
    }

    private void OpenFromInventory()
    {
        Open();
        _fromInventory = true;
    }

    private void OnFinished(CharacterLook look)
    {
        PlayerLookStore.Save(look);
        BackToInventory();
    }

    private void OnCancelled() => BackToInventory();

    private void BackToInventory()
    {
        if (_fromInventory)
        {
            _fromInventory = false;
            Inventory?.CallDeferred(InventoryScreen.MethodName.Open);
        }
    }
}
