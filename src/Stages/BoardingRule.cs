using Godot;
using ReusableGameSystems.Conditions;

namespace DontBeLate.Stages;

/// <summary>
/// Something the bus driver notices when you try to get on: "soaked to the bone", "no shoes". When
/// <see cref="When"/> is met the driver says <see cref="DriverLine"/>; with <see cref="Refuse"/> they also
/// don't let you on.
/// </summary>
[GlobalClass]
public partial class BoardingRule : Resource
{
    [Export]
    public string Id { get; set; } = "";

    [Export]
    public Condition? When { get; set; }

    [Export(PropertyHint.MultilineText)]
    public string DriverLine { get; set; } = "";

    [Export]
    public bool Refuse { get; set; } = true;
}
