using DontBeLate.DebugCommands;
using Godot;
using ReusableGameSystems.CityGen;
using ReusableGameSystems.Randomness;
using ReusableGameSystems.Runs;

namespace DontBeLate.City;

/// <summary>
/// Builds the city for the current run (a new one each run, or the kept one; see <see cref="CityStore"/>) and
/// puts the player outside their home. Waits for the run to start so the city comes from the right seed.
/// </summary>
[GlobalClass]
public partial class CityStage : Node3D
{
    private RunManager? _runs;

    [Signal]
    public delegate void CityBuiltEventHandler(string seedCode);

    [Export]
    public CityBuilder? City { get; set; }

    /// <summary>Moved to <see cref="SpawnLandmark"/> after each build.</summary>
    [Export]
    public Node3D? Player { get; set; }

    [Export]
    public string SpawnLandmark { get; set; } = "home";

    /// <summary>Start a run if none is going (for scenes without a director, like the preview).</summary>
    [Export]
    public bool StartRunIfIdle { get; set; } = true;

    public static CityStage? Current { get; private set; }

    public CitySettings Settings { get; private set; } = new();

    public CityLayout? Layout => City?.Layout;

    public string SeedCode => Layout != null ? Seeds.ToCode(Layout.Seed) : "";

    public override void _Ready()
    {
        Current = this;
        CityCommands.Register();
        CityLifeCommands.Register();
        Settings = CityStore.Load();
        _runs = RunManager.Instance;
        if (_runs == null)
        {
            Build(Seeds.NewRandom());
            return;
        }

        _runs.Loop.Started += OnRunStarted;
        if (_runs.Loop.IsRunning)
        {
            Build(Settings.SeedFor(_runs.Loop.Seed));
        }
        else if (StartRunIfIdle)
        {
            _runs.StartRun();
        }
    }

    public override void _ExitTree()
    {
        if (_runs != null)
        {
            _runs.Loop.Started -= OnRunStarted;
        }

        if (Current == this)
        {
            Current = null;
        }
    }

    /// <summary>Builds the city for <paramref name="seed"/> and puts the player at the spawn landmark.</summary>
    public void Build(ulong seed)
    {
        if (City == null)
        {
            return;
        }

        City.Build(seed);
        PlacePlayer();
        EmitSignal(SignalName.CityBuilt, SeedCode);
    }

    /// <summary>A brand new city right now. If a city is kept, the new one becomes the kept one.</summary>
    public void NewCity()
    {
        ulong seed = Seeds.NewRandom();
        if (Settings.Mode == CitySeedMode.Kept)
        {
            Settings.KeptSeed = Seeds.ToCode(seed);
            CityStore.Save(Settings);
        }

        Build(seed);
    }

    /// <summary>Keeps the current city for every run from now on.</summary>
    public void KeepCurrent()
    {
        Settings.Mode = CitySeedMode.Kept;
        Settings.KeptSeed = SeedCode;
        CityStore.Save(Settings);
    }

    public void SetMode(CitySeedMode mode)
    {
        Settings.Mode = mode;
        if (mode == CitySeedMode.Kept && string.IsNullOrEmpty(Settings.KeptSeed))
        {
            Settings.KeptSeed = SeedCode;
        }

        CityStore.Save(Settings);
    }

    /// <summary>Puts the player on the pavement in front of a landmark, facing the road. False if there's no such landmark.</summary>
    public bool MovePlayerTo(string landmarkId)
    {
        var layout = Layout;
        var landmark = layout?.FindLandmark(landmarkId);
        if (Player == null || layout == null || landmark == null)
        {
            return false;
        }

        // Out of the door (or off the bus stop) a step toward the road.
        var spot = landmark.Position + (landmark.Facing * (landmark.Rule.Placement == LandmarkPlacement.Lot ? 1.5f : 0.6f));
        var position = CityLayout.ToWorld(spot, layout.GroundHeight + 0.1f);
        if (Player is CharacterBody3D body)
        {
            body.Velocity = Vector3.Zero;
        }

        Player.GlobalTransform = new Transform3D(CityLayout.FacingBasis(landmark.Facing), position);
        return true;
    }

    /// <summary>Puts the player on the beach in the middle of the longest coast side, facing the sea. False without a coast.</summary>
    public bool MovePlayerToShore()
    {
        var around = Layout?.Surroundings;
        if (Player == null || Layout == null || around == null || !around.HasCoast)
        {
            return false;
        }

        int best = -1;
        for (int i = 0; i < around.Outline.Count; i++)
        {
            if (around.IsCoast(i) && (best < 0 || around.Outline.EdgeLength(i) > around.Outline.EdgeLength(best)))
            {
                best = i;
            }
        }

        var outward = around.Outward(best);
        var spot = around.Outline.EdgeMiddle(best) + (outward * (Layout.Style.PavementWidth + 1f));
        if (Player is CharacterBody3D body)
        {
            body.Velocity = Vector3.Zero;
        }

        Player.GlobalTransform = new Transform3D(CityLayout.FacingBasis(outward), CityLayout.ToWorld(spot, Layout.GroundHeight + 0.3f));
        return true;
    }

    private void OnRunStarted(RunStarted started)
    {
        Build(Settings.SeedFor(started.Seed));
    }

    private void PlacePlayer()
    {
        if (!MovePlayerTo(SpawnLandmark) && Player != null && Layout != null)
        {
            var any = Layout.Landmarks.Count > 0 ? Layout.Landmarks[0].Id : null;
            if (any != null)
            {
                MovePlayerTo(any);
            }
        }
    }
}
