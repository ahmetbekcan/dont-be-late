namespace DontBeLate.City;

/// <summary>Which city a morning takes place in.</summary>
public enum CitySeedMode
{
    /// <summary>A new city every run, made from the run's seed (replaying a run replays its city).</summary>
    NewEachRun,

    /// <summary>The same kept city every run, so you can learn your way around.</summary>
    Kept,
}
