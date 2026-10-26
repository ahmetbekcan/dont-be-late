using Godot;
using ReusableGameSystems.Phone;
using ReusableGameSystems.WorldState;

namespace DontBeLate.PhoneApps;

/// <summary>Today's weather from the world's facts: a big picture, a heading and a hint.</summary>
public partial class WeatherAppView : PhoneAppView
{
    /// <summary>True if it showed rain (for tests).</summary>
    public bool ShowsRain { get; private set; }

    public string HeadingText { get; private set; } = "";

    public override void OnOpened()
    {
        if (App is not WeatherPhoneApp app)
        {
            return;
        }

        ShowsRain = WorldStateStore.Instance?.Facts.GetBool(app.RainFact) ?? false;
        HeadingText = ShowsRain ? app.RainHeading : app.DryHeading;
        var center = new CenterContainer();
        AddChild(center);
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var column = new VBoxContainer { CustomMinimumSize = new Vector2(220, 0) };
        column.AddThemeConstantOverride("separation", 8);
        center.AddChild(column);
        column.AddChild(new TextureRect
        {
            Texture = ShowsRain ? app.RainPicture : app.DryPicture,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            CustomMinimumSize = new Vector2(96, 96),
        });
        AddLabel(column, HeadingText, PhoneScreen.TitleVariation).HorizontalAlignment = HorizontalAlignment.Center;
        AddLabel(column, ShowsRain ? app.RainText : app.DryText, PhoneScreen.MutedVariation).HorizontalAlignment = HorizontalAlignment.Center;
    }

    /// <summary>The weather may have turned while the phone was away.</summary>
    public override void OnShown()
    {
        FreeChildren(this);
        OnOpened();
    }
}
