using Godot;
using ReusableGameSystems.Phone;

namespace DontBeLate.PhoneApps;

/// <summary>The phone's weather app: reads the morning's facts (e.g. <c>raining</c>) and says what to expect.</summary>
[GlobalClass]
public partial class WeatherPhoneApp : PhoneApp
{
    /// <summary>The fact that means it's raining.</summary>
    [Export]
    public string RainFact { get; set; } = "raining";

    [Export]
    public Texture2D? RainPicture { get; set; }

    [Export]
    public Texture2D? DryPicture { get; set; }

    [Export]
    public string RainHeading { get; set; } = "Rain all morning";

    [Export(PropertyHint.MultilineText)]
    public string RainText { get; set; } = "Puddles everywhere. Umbrella recommended.";

    [Export]
    public string DryHeading { get; set; } = "Dry and mild";

    [Export(PropertyHint.MultilineText)]
    public string DryText { get; set; } = "No rain expected.";

    public override PhoneAppView CreateView() => new WeatherAppView();
}
