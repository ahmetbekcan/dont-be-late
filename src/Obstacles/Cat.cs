using System;
using Godot;
using ReusableGameSystems.Characters;
using ReusableGameSystems.Conditions;
using ReusableGameSystems.Events;
using ReusableGameSystems.Interaction;
using ReusableGameSystems.Npcs;
using ReusableGameSystems.Randomness;
using ReusableGameSystems.StatusEffects;
using ReusableGameSystems.WorldState;

namespace DontBeLate.Obstacles;

/// <summary>What the cat is doing.</summary>
public enum CatState
{
    Sleeping,
    GuardingShoes,
    GoingToFood,
    Eating,
    Fleeing,
}

/// <summary>
/// The cat. It isn't scripted per solution; it just behaves:
/// <list type="bullet">
/// <item>When <see cref="GuardFact"/> is set it sits on the shoes. Grabbing them from under it may work, or it swipes at you.</item>
/// <item>It goes for food (bodies in <see cref="FoodGroup"/>) wherever it is, follows food you carry, and eats food left on the floor.</item>
/// <item>Anything thrown at it sends it running to its bed.</item>
/// </list>
/// The cat clears <see cref="GuardFact"/> whenever it leaves the shoes, which is what makes them wearable.
/// </summary>
[GlobalClass]
public partial class Cat : CharacterBody3D
{
    private ProximitySensor? _sensor;
    private Node3D? _food;
    private double _eatTimer;
    private Blackboard? _facts;
    private LogicContext? _context;

    [Signal]
    public delegate void StateChangedEventHandler(CatState state);

    [Export]
    public NpcMover? Mover { get; set; }

    /// <summary>Notices food. Its <c>Group</c> should be <see cref="FoodGroup"/>.</summary>
    [Export]
    public ProximitySensor? FoodSensor { get; set; }

    /// <summary>Things hitting this area fast enough scare the cat.</summary>
    [Export]
    public Area3D? HitBox { get; set; }

    [Export]
    public Node3D? Bed { get; set; }

    [Export]
    public Node3D? ShoesSpot { get; set; }

    /// <summary>The shoes' interactables; using <see cref="GrabVerb"/> on them while guarded tests the cat's patience.</summary>
    [Export]
    public Godot.Collections.Array<Interactable> Shoes { get; set; } = new();

    [Export]
    public string GuardFact { get; set; } = "cat_on_shoes";

    [Export]
    public string GrabVerb { get; set; } = "grab";

    /// <summary>The verb that puts the shoes on once the cat is gone.</summary>
    [Export]
    public string WearVerb { get; set; } = "wear";

    [Export]
    public string FoodGroup { get; set; } = "cat_food";

    [Export(PropertyHint.Range, "0,1,0.01")]
    public double GrabSuccessChance { get; set; } = 0.35;

    [Export(PropertyHint.Range, "0,60,0.5")]
    public double EatSeconds { get; set; } = 12;

    /// <summary>Speed of a thrown object that scares the cat.</summary>
    [Export(PropertyHint.Range, "0,30,0.1")]
    public float ScareSpeed { get; set; } = 3f;

    [Export(PropertyHint.Range, "0,20,0.1")]
    public float SwipeKnockback { get; set; } = 5f;

    /// <summary>Applied to whoever gets swiped (e.g. stunned for a moment).</summary>
    [Export]
    public StatusEffectDefinition? SwipeStatus { get; set; }

    [Export]
    public StatusEffectDefinition? ScratchedStatus { get; set; }

    [Export]
    public float WalkSpeed { get; set; } = 1.6f;

    [Export]
    public float RunSpeed { get; set; } = 5f;

    public CatState State { get; private set; } = CatState.Sleeping;

    public LogicContext Context
    {
        get => _context ??= LogicContext.FromGlobals("cat");
        set => _context = value;
    }

    public override void _Ready()
    {
        _sensor = FoodSensor;
        if (_sensor != null)
        {
            _sensor.Group = FoodGroup;
            _sensor.Detected += OnFoodDetected;
        }

        if (HitBox != null)
        {
            HitBox.BodyEntered += OnHit;
        }

        if (Mover != null)
        {
            Mover.Arrived += OnArrived;
        }

        foreach (var shoe in Shoes)
        {
            if (shoe != null)
            {
                shoe.InteractedWith += OnShoesUsed;
            }
        }

        _facts = Context.Facts;
        if (_facts != null)
        {
            _facts.Changed += OnFactChanged;
            if (_facts.GetBool(GuardFact))
            {
                SitOnShoes();
            }
        }
    }

    public override void _ExitTree()
    {
        if (_facts != null)
        {
            _facts.Changed -= OnFactChanged;
        }

        foreach (var shoe in Shoes)
        {
            if (shoe != null && IsInstanceValid(shoe))
            {
                shoe.InteractedWith -= OnShoesUsed;
            }
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        switch (State)
        {
            case CatState.GoingToFood:
                if (_food == null || !IsInstanceValid(_food))
                {
                    Settle();
                }
                else
                {
                    Mover?.MoveTo(_food.GlobalPosition);
                }

                break;

            case CatState.Eating:
                if (_food == null || !IsInstanceValid(_food) || IsHeld(_food) || _food.GlobalPosition.DistanceTo(GlobalPosition) > 1.2f)
                {
                    // Someone took the food away: go after it.
                    State = CatState.GoingToFood;
                    break;
                }

                _eatTimer -= delta;
                if (_eatTimer <= 0)
                {
                    _food.QueueFree();
                    _food = null;
                    EventBus.Instance?.EmitEvent("toast", "The cat finishes the tuna and curls up for a nap.");
                    Settle();
                }

                break;
        }
    }

    /// <summary>Puts the cat on the shoes (the morning's scenario does this through <see cref="GuardFact"/>).</summary>
    public void SitOnShoes()
    {
        if (ShoesSpot != null)
        {
            GlobalPosition = ShoesSpot.GlobalPosition;
        }

        Mover?.Stop();
        SetState(CatState.GuardingShoes);
    }

    /// <summary>Something scared the cat: it bolts for its bed.</summary>
    public void Scare()
    {
        if (State == CatState.Fleeing)
        {
            return;
        }

        LeaveShoes();
        EventBus.Instance?.EmitEvent("toast", "MRRROWW! The cat bolts for its bed.");
        _food = null;
        if (Mover != null && Bed != null)
        {
            Mover.Speed = RunSpeed;
            Mover.MoveTo(Bed.GlobalPosition);
        }

        SetState(CatState.Fleeing);
    }

    private void SetState(CatState state)
    {
        State = state;
        if (_sensor != null)
        {
            _sensor.Active = state is CatState.Sleeping or CatState.GuardingShoes or CatState.GoingToFood;
        }

        EmitSignal(SignalName.StateChanged, (int)state);
    }

    private void LeaveShoes()
    {
        if (State == CatState.GuardingShoes)
        {
            _facts?.Set(GuardFact, false);
        }
    }

    private void Settle()
    {
        Mover?.Stop();
        _food = null;
        SetState(CatState.Sleeping);
    }

    private void OnFactChanged(string key, Variant oldValue, Variant newValue)
    {
        if (key == GuardFact && newValue.VariantType == Variant.Type.Bool && newValue.AsBool() && State != CatState.GuardingShoes)
        {
            SitOnShoes();
        }
    }

    private void OnFoodDetected(Node3D food)
    {
        if (State is CatState.Fleeing or CatState.Eating)
        {
            return;
        }

        if (State == CatState.GuardingShoes)
        {
            EventBus.Instance?.EmitEvent("toast", "The cat sniffs the air... tuna?");
        }

        LeaveShoes();
        _food = food;
        if (Mover != null)
        {
            Mover.Speed = WalkSpeed;
        }

        SetState(CatState.GoingToFood);
    }

    private void OnArrived()
    {
        switch (State)
        {
            case CatState.Fleeing:
                Settle();
                break;
            case CatState.GoingToFood when _food != null && IsInstanceValid(_food) && !IsHeld(_food):
                Mover?.Stop();
                _eatTimer = EatSeconds;
                SetState(CatState.Eating);
                break;
        }
    }

    private void OnHit(Node3D body)
    {
        if (body is RigidBody3D rigid && rigid.LinearVelocity.Length() >= ScareSpeed && !rigid.IsInGroup(FoodGroup))
        {
            Scare();
        }
    }

    private void OnShoesUsed(Interactable shoes, InteractionVerb verb, Node? actor)
    {
        if (verb.Id != GrabVerb || State != CatState.GuardingShoes)
        {
            return;
        }

        var random = RandomService.Instance?.Stream("cat");
        double roll = random?.NextDouble() ?? GD.Randf();
        var effects = Context.Get<StatusEffectSet>();
        if (roll < GrabSuccessChance)
        {
            if (ScratchedStatus != null)
            {
                effects?.Apply(ScratchedStatus);
            }

            Scare();
            EventBus.Instance?.EmitEvent("toast", "You yank the shoes out from under the cat. It takes some skin with it.");
            shoes.Interact(WearVerb, actor, Context);
            return;
        }

        EventBus.Instance?.EmitEvent("toast", "The cat swipes at you. HISS. It's not moving.");
        if (SwipeStatus != null)
        {
            effects?.Apply(SwipeStatus);
        }

        if (ScratchedStatus != null)
        {
            effects?.Apply(ScratchedStatus);
        }

        if (actor is CharacterMotor motor)
        {
            var away = motor.GlobalPosition - GlobalPosition;
            away.Y = 0;
            motor.AddImpulse((away.LengthSquared() > 0.001f ? away.Normalized() : Vector3.Back) * SwipeKnockback + (Vector3.Up * 2f));
        }
    }

    private static bool IsHeld(Node3D food)
    {
        foreach (var child in food.GetChildren())
        {
            if (child is Carryable carryable)
            {
                return carryable.IsHeld;
            }
        }

        return false;
    }
}
