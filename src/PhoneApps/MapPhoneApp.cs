using Godot;
using ReusableGameSystems.Phone;

namespace DontBeLate.PhoneApps;

/// <summary>The phone's map of the generated town: where you are, the landmarks, and the walk to one.</summary>
[GlobalClass]
public partial class MapPhoneApp : PhoneApp
{
    /// <summary>Shown where there's no generated town (yet).</summary>
    [Export]
    public string EmptyHeading { get; set; } = "No map here";

    [Export(PropertyHint.MultilineText)]
    public string EmptyBody { get; set; } = "This place isn't on the map yet.";

    /// <summary>Walking speed for the time estimates, in meters per second.</summary>
    [Export(PropertyHint.Range, "0.5,10,0.1")]
    public float WalkSpeed { get; set; } = 4.5f;

    public override PhoneAppView? CreateView() => new MapAppView
    {
        EmptyHeading = EmptyHeading,
        EmptyBody = EmptyBody,
        WalkSpeed = WalkSpeed,
    };
}
