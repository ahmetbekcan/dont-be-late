using Godot;
using ReusableGameSystems.Phone;

namespace DontBeLate.PhoneApps;

/// <summary>The phone's bus app: the stop's live timetable (see <see cref="BusesAppView"/>).</summary>
[GlobalClass]
public partial class BusesPhoneApp : PhoneApp
{
    /// <summary>Name of the stop, shown at the top.</summary>
    [Export]
    public string StopName { get; set; } = "Your stop";

    public override PhoneAppView CreateView() => new BusesAppView { StopName = StopName };
}
