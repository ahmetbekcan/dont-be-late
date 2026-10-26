using System;
using Godot;
using ReusableGameSystems.Appearance;

namespace DontBeLate.Characters;

/// <summary>
/// The player's character look: saved in <see cref="SavePath"/> once made in the creator, otherwise the
/// default look from <see cref="DefaultPath"/>.
/// </summary>
public static class PlayerLookStore
{
    public const string DefaultPath = "res://content/characters/looks/player.tres";
    public const string SavePath = "user://player_look.tres";

    /// <summary>Raised with the new look after <see cref="Save"/>.</summary>
    public static event Action<CharacterLook>? Changed;

    /// <summary>Where saves go (tests point it elsewhere).</summary>
    public static string Path { get; set; } = SavePath;

    public static CharacterLook Load()
    {
        if (ResourceLoader.Exists(Path) && ResourceLoader.Load<CharacterLook>(Path, cacheMode: ResourceLoader.CacheMode.Ignore) is { } saved)
        {
            return saved;
        }

        return ResourceLoader.Load<CharacterLook>(DefaultPath).Copy();
    }

    public static Error Save(CharacterLook look)
    {
        var error = ResourceSaver.Save(look, Path);
        if (error == Error.Ok)
        {
            Changed?.Invoke(look);
        }

        return error;
    }
}
