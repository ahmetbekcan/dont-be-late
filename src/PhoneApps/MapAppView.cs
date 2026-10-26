using System.Collections.Generic;
using Godot;
using ReusableGameSystems.CityGen;
using ReusableGameSystems.Clock;
using ReusableGameSystems.Phone;

namespace DontBeLate.PhoneApps;

/// <summary>
/// The map of the generated town (<see cref="CityBuilder.Current"/>) filling the phone: you as an arrow, round
/// place buttons along the top, and once you pick a place, the walking route there with its distance and time
/// in a card at the bottom.
/// </summary>
public partial class MapAppView : PhoneAppView
{
    public const string PlaceButtonVariation = "MapPlaceButton";
    public const string PlaceCardVariation = "MapPlaceCard";

    private const double RefreshSeconds = 0.5;
    private const float ChipsHeight = 58;
    private const float CardHeight = 74;

    private readonly ButtonGroup _chipGroup = new();
    private CityLayout? _city;
    private Node3D? _player;
    private Label _placeName = null!;
    private Label _placeDetail = null!;
    private PanelContainer _placeCard = null!;
    private double _sinceRefresh;

    public string EmptyHeading { get; set; } = "No map here";

    public string EmptyBody { get; set; } = "";

    /// <summary>Meters per second, for the walking times.</summary>
    public float WalkSpeed { get; set; } = 4.5f;

    /// <summary>Walks shorter than this count as "you're here".</summary>
    public float HereDistance { get; set; } = 12f;

    public CityMapView? Map { get; private set; }

    /// <summary>The picked landmark id, or null.</summary>
    public string? Selected { get; private set; }

    /// <summary>Walking distance to the picked landmark, in meters (0 if none).</summary>
    public float RouteLength { get; private set; }

    /// <summary>The card's lines, e.g. "Bank" and "200 m · 2 min walk" (for tests).</summary>
    public string PlaceText => $"{_placeName?.Text}\n{_placeDetail?.Text}";

    public override void OnOpened()
    {
        _city = CityBuilder.Current?.Layout;
        if (_city == null)
        {
            var list = AddList();
            AddLabel(list, EmptyHeading, PhoneScreen.TitleVariation);
            AddLabel(list, EmptyBody, PhoneScreen.MutedVariation);
            return;
        }

        _player = GetTree().GetFirstNodeInGroup("player") as Node3D;
        Map = new CityMapView
        {
            Name = "Map",
            Layout = _city,
            Tracked = _player,
            PinRadius = 11,
            ShowLabels = false,
        };
        Map.LandmarkPressed += Select;
        AddChild(Map);
        Map.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var chips = new HBoxContainer { Name = "Places", Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = MouseFilterEnum.Ignore };
        chips.AddThemeConstantOverride("separation", 8);
        AddChild(chips);
        chips.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
        chips.OffsetTop = 10;
        chips.OffsetBottom = ChipsHeight;
        foreach (var landmark in _city.Landmarks)
        {
            AddChip(chips, landmark);
        }

        _placeCard = new PanelContainer { Name = "Place", ThemeTypeVariation = PlaceCardVariation, Visible = false };
        AddChild(_placeCard);
        _placeCard.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide);
        _placeCard.OffsetLeft = 10;
        _placeCard.OffsetRight = -10;
        _placeCard.OffsetTop = -CardHeight - 10;
        _placeCard.OffsetBottom = -10;
        var box = new VBoxContainer();
        _placeCard.AddChild(box);
        _placeName = AddLabel(box, "", PhoneScreen.TitleVariation);
        _placeDetail = AddLabel(box, "", PhoneScreen.AccentVariation);

        // Start on where you are.
        Callable.From(ShowPlayer).CallDeferred();
    }

    public override void _Process(double delta)
    {
        _sinceRefresh += delta;
        if (_sinceRefresh >= RefreshSeconds)
        {
            _sinceRefresh = 0;
            Refresh();
        }
    }

    /// <summary>Picks a landmark: shows the walk there and frames you and it on the map.</summary>
    public void Select(string id)
    {
        if (_city?.FindLandmark(id) is not { } landmark || Map == null)
        {
            return;
        }

        Selected = id;
        Map.Selected = id;
        foreach (var node in _chipGroup.GetButtons())
        {
            if (node.HasMeta("landmark"))
            {
                node.SetPressedNoSignal(node.GetMeta("landmark").AsString() == id);
            }
        }

        Refresh();
        var area = new Rect2(landmark.Position, Vector2.Zero);
        if (Map.Route != null)
        {
            foreach (var point in Map.Route)
            {
                area = area.Expand(point);
            }
        }

        Map.FitArea(area.Grow(8), 16, ChipsHeight, CardHeight + 10);
    }

    /// <summary>"240 m · 3 min walk" for a walk of <paramref name="meters"/>, in game minutes.</summary>
    public string DescribeWalk(float meters)
    {
        if (meters < HereDistance)
        {
            return "You're here";
        }

        double scale = GameClock.Instance?.TimeScale ?? 1;
        int minutes = Mathf.Max(1, Mathf.RoundToInt(meters / Mathf.Max(0.1f, WalkSpeed) * scale / 60.0));
        string distance = meters >= 1000 ? $"{meters / 1000:0.0} km" : $"{Mathf.RoundToInt(meters / 10) * 10} m";
        return $"{distance} · {minutes} min walk";
    }

    /// <summary>The walk from the player to a landmark: plan points from the player, and its length.</summary>
    public List<Vector2>? RouteTo(PlacedLandmark landmark, out float length)
    {
        length = 0;
        if (_city == null || _player == null || landmark.WalkNode < 0)
        {
            return null;
        }

        var here = CityLayout.ToPlan(_player.GlobalPosition);
        var path = _city.Walk.FindPath(_city.Walk.Nearest(here), landmark.WalkNode);
        if (path.Count == 0)
        {
            return null;
        }

        var points = _city.Walk.PathPoints(path);
        points.Insert(0, here);
        length = here.DistanceTo(points[1]) + _city.Walk.PathLength(path);
        return points;
    }

    private void Refresh()
    {
        if (_city == null || Map == null || Selected == null || _city.FindLandmark(Selected) is not { } landmark)
        {
            return;
        }

        var route = RouteTo(landmark, out float length);
        RouteLength = length;
        Map.Route = route != null && length >= HereDistance ? route : null;
        _placeName.Text = landmark.DisplayName;
        _placeDetail.Text = route != null ? DescribeWalk(length) : "No way there from here";
        _placeCard.Visible = true;
    }

    private void ShowPlayer()
    {
        if (Map == null || _player == null || !IsInstanceValid(_player))
        {
            return;
        }

        // Around you, nudged inward so the view shows town rather than the grass beyond it.
        var here = CityLayout.ToPlan(_player.GlobalPosition);
        var area = new Rect2(here, Vector2.Zero).Grow(70);
        var town = Map.TownArea();
        var shift = Vector2.Zero;
        if (town.Size.X > area.Size.X)
        {
            shift.X = Mathf.Max(0, town.Position.X - area.Position.X) - Mathf.Max(0, area.End.X - town.End.X);
        }

        if (town.Size.Y > area.Size.Y)
        {
            shift.Y = Mathf.Max(0, town.Position.Y - area.Position.Y) - Mathf.Max(0, area.End.Y - town.End.Y);
        }

        Map.FitArea(new Rect2(area.Position + shift, area.Size), 16, ChipsHeight, 10);
    }

    private void AddChip(HBoxContainer chips, PlacedLandmark landmark)
    {
        var button = new Button
        {
            Name = landmark.Id,
            ThemeTypeVariation = PlaceButtonVariation,
            Icon = landmark.Rule.Icon,
            ExpandIcon = true,
            ToggleMode = true,
            ButtonGroup = _chipGroup,
            CustomMinimumSize = new Vector2(46, 46),
            TooltipText = landmark.DisplayName,
            FocusMode = FocusModeEnum.None,
        };
        button.SetMeta("landmark", landmark.Id);
        button.Pressed += () => Select(landmark.Id);
        chips.AddChild(button);

        // The theme gives the shape; each place rings it in its own map colour.
        foreach (string state in new[] { "normal", "hover", "pressed" })
        {
            if (button.GetThemeStylebox(state) is StyleBoxFlat flat)
            {
                var tinted = (StyleBoxFlat)flat.Duplicate();
                tinted.BorderColor = landmark.Rule.MapColor;
                button.AddThemeStyleboxOverride(state, tinted);
            }
        }
    }
}
