using Godot;
using ReusableGameSystems.CityLife;
using ReusableGameSystems.Characters;
using ReusableGameSystems.Conditions;
using ReusableGameSystems.Events;
using ReusableGameSystems.StatusEffects;

namespace DontBeLate.City;

/// <summary>
/// How the game answers traffic: a honk is a toast; a car hitting you knocks you aside and bruises you (and a
/// hard hit leaves you limping). No time penalty and no failing: walking slower is the cost.
/// </summary>
[GlobalClass]
public partial class TrafficReactions : Node
{
    private LogicContext? _context;
    private CitySimulation? _simulation;
    private double _lastHonkToast = -100;

    [Export]
    public CitySimulation? Simulation { get; set; }

    /// <summary>Pushed by hits.</summary>
    [Export]
    public CharacterMotor? Player { get; set; }

    [Export]
    public StatusEffectDefinition? Bruised { get; set; }

    /// <summary>Added on top for hits at <see cref="HardHitSpeed"/> or faster.</summary>
    [Export]
    public StatusEffectDefinition? Limping { get; set; }

    /// <summary>m/s.</summary>
    [Export(PropertyHint.Range, "0,40,0.5")]
    public float HardHitSpeed { get; set; } = 9f;

    /// <summary>Sideways shove per m/s of the car's speed.</summary>
    [Export(PropertyHint.Range, "0,5,0.05")]
    public float Push { get; set; } = 0.9f;

    /// <summary>Upward pop on a hit, m/s.</summary>
    [Export(PropertyHint.Range, "0,20,0.1")]
    public float Lift { get; set; } = 4f;

    /// <summary>Fewest seconds between honk toasts.</summary>
    [Export(PropertyHint.Range, "0,30,0.5")]
    public double HonkToastGap { get; set; } = 5;

    [Export]
    public string[] HonkLines { get; set; } =
    {
        "HONK! \"Get out of the road!\"",
        "BEEEEP! The driver waves a fist.",
        "HONK HONK! Someone's in a hurry.",
        "A long, angry HOOONK.",
    };

    private LogicContext Context => _context ??= LogicContext.FromGlobals("traffic");

    public override void _Ready()
    {
        _simulation = Simulation ?? CitySimulation.Current;
        if (_simulation == null)
        {
            return;
        }

        _simulation.Honked += OnHonked;
        _simulation.Hit += OnHit;
    }

    public override void _ExitTree()
    {
        if (_simulation != null)
        {
            _simulation.Honked -= OnHonked;
            _simulation.Hit -= OnHit;
        }
    }

    /// <summary>What happens when a car hits the player (also used by the debug console).</summary>
    public void HitPlayer(float speed, Vector2 heading)
    {
        if (Player != null)
        {
            var away = new Vector3(heading.X, 0, -heading.Y).Normalized();
            Player.AddImpulse((away * speed * Push) + (Vector3.Up * Lift));
        }

        var effects = Context.Get<StatusEffectSet>();
        if (Bruised != null)
        {
            effects?.Apply(Bruised);
        }

        if (Limping != null && speed >= HardHitSpeed)
        {
            effects?.Apply(Limping);
        }
    }

    /// <summary>A honk toast (throttled so a queue of angry drivers doesn't spam).</summary>
    public void Honk(int variant)
    {
        double now = Time.GetTicksMsec() / 1000.0;
        if (now - _lastHonkToast < HonkToastGap || HonkLines.Length == 0)
        {
            return;
        }

        _lastHonkToast = now;
        EventBus.Instance?.EmitEvent("toast", HonkLines[Mathf.PosMod(variant, HonkLines.Length)]);
    }

    private void OnHonked(CarState car) => Honk(car.Look);

    private void OnHit(CarState car, float speed, Vector2 heading) => HitPlayer(speed, heading);
}
