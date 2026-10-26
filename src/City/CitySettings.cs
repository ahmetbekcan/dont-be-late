using Godot;
using ReusableGameSystems.Randomness;

namespace DontBeLate.City;

/// <summary>The player's city choice: a new city every run, or one kept city (by its seed code).</summary>
[GlobalClass]
public partial class CitySettings : Resource
{
    [Export]
    public CitySeedMode Mode { get; set; } = CitySeedMode.NewEachRun;

    /// <summary>Seed code of the kept city (used when <see cref="Mode"/> is <see cref="CitySeedMode.Kept"/>).</summary>
    [Export]
    public string KeptSeed { get; set; } = "";

    /// <summary>The city seed for a run: the kept city, or one made from the run's seed.</summary>
    public ulong SeedFor(ulong runSeed)
    {
        if (Mode == CitySeedMode.Kept && !string.IsNullOrWhiteSpace(KeptSeed))
        {
            return Seeds.Parse(KeptSeed);
        }

        return Seeds.Derive(runSeed, "city");
    }
}
