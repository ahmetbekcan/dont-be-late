using System;
using Godot;

namespace DontBeLate.City;

/// <summary>Saves the player's <see cref="CitySettings"/> in <see cref="SavePath"/> (a new city each run until they keep one).</summary>
public static class CityStore
{
    public const string SavePath = "user://city.tres";

    /// <summary>Raised with the new settings after <see cref="Save"/>.</summary>
    public static event Action<CitySettings>? Changed;

    /// <summary>Where saves go (tests point it elsewhere).</summary>
    public static string Path { get; set; } = SavePath;

    public static CitySettings Load()
    {
        if (ResourceLoader.Exists(Path) && ResourceLoader.Load<CitySettings>(Path, cacheMode: ResourceLoader.CacheMode.Ignore) is { } saved)
        {
            return saved;
        }

        return new CitySettings();
    }

    public static Error Save(CitySettings settings)
    {
        var error = ResourceSaver.Save(settings, Path);
        if (error == Error.Ok)
        {
            Changed?.Invoke(settings);
        }

        return error;
    }
}
