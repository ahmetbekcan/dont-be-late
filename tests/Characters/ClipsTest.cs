namespace DontBeLate.Tests.Characters;

using System.Threading.Tasks;
using GdUnit4;
using Godot;
using ReusableGameSystems.Appearance;
using ReusableGameSystems.DebugTools;
using ReusableGameSystems.Tests;
using static GdUnit4.Assertions;

/// <summary>The animation clips baked from the Quaternius library, and the walkers that play them.</summary>
[TestSuite]
[RequireGodotRuntime]
public class ClipsTest
{
    private const string Clips = "res://content/characters/clips/";
    private const string WalkerPath = "res://scenes/characters/walker.tscn";

    private static CharacterClip Load(string id) => GD.Load<CharacterClip>(Clips + id + ".tres");

    [TestCase]
    public void TheBakedClipsMoveEveryJointAtSensibleSpeeds()
    {
        foreach (string id in new[] { "idle", "walk", "walk_formal", "jog", "sprint", "sit", "sit_talk", "talk", "startled", "interact", "dance" })
        {
            var clip = Load(id);
            AssertString(clip.Id).IsEqual(id);
            AssertFloat(clip.Length).IsGreater(0f);
            for (int j = 0; j < CharacterJoints.All.Length; j++)
            {
                AssertBool(clip.Moves(j)).IsTrue();
            }
        }

        // A walk is a walking pace; runs are faster; the rest stay on the spot.
        AssertFloat(Load("walk").NativeSpeed).IsBetween(0.9f, 1.3f);
        AssertFloat(Load("jog").NativeSpeed).IsGreater(Load("walk").NativeSpeed * 2);
        AssertFloat(Load("sprint").NativeSpeed).IsGreater(Load("jog").NativeSpeed);
        AssertFloat(Load("idle").NativeSpeed).IsEqual(0f);
        AssertFloat(Load("sit").NativeSpeed).IsEqual(0f);

        // A flinch plays once, over the upper body.
        AssertBool(Load("startled").Loop).IsFalse();
        AssertBool(Load("startled").UpperBodyOnly).IsTrue();
        AssertBool(Load("walk").Loop).IsTrue();
    }

    [TestCase]
    public async Task AWalkerWalksWithTheClipsAndFlinches()
    {
        var walker = GD.Load<PackedScene>(WalkerPath).Instantiate<Node3D>();
        using var runner = ISceneRunner.Load(walker, autoFree: true);
        var clips = walker.GetNode<CharacterClips>("Body/Clips");
        var body = walker.GetNode<CharacterBody>("Body");
        await runner.Step(10);
        AssertFloat(clips.Blend.X).IsEqualApprox(1f, 0.001f);

        // At a commuter's pace: walking, and the legs swing.
        clips.Speed = 1.6f;
        await runner.StepSeconds(1);
        AssertFloat(clips.Blend.Y).IsEqualApprox(1f, 0.001f);
        var hip = body.GetJoint(CharacterJoints.HipL)!;
        float low = float.MaxValue;
        float high = float.MinValue;
        for (int i = 0; i < 20; i++)
        {
            await runner.Step(2);
            float swing = hip.Rotation.X;
            low = Mathf.Min(low, swing);
            high = Mathf.Max(high, swing);
        }

        AssertFloat(high - low).IsGreater(Mathf.DegToRad(20));

        // Startled (a crowd feeling of the same name).
        AssertBool(clips.Play("startled")).IsTrue();
        AssertString(clips.Action).IsEqual("startled");
        await runner.StepSeconds(1);
        AssertString(clips.Action).IsEqual("");
    }

    [TestCase]
    public async Task TheAnimCommandSwitchesClipsOffAndOn()
    {
        var walker = GD.Load<PackedScene>(WalkerPath).Instantiate<Node3D>();
        using var runner = ISceneRunner.Load(walker, autoFree: true);
        var clips = walker.GetNode<CharacterClips>("Body/Clips");
        var registry = new CommandRegistry();
        DontBeLate.DebugCommands.ClipCommands.Register(registry);
        await runner.Step(5);

        AssertString(registry.Execute("anim off").Output).Contains("off");
        AssertFloat(clips.Weight).IsEqual(0f);
        await runner.StepSeconds(1);
        AssertFloat(clips.CurrentWeight).IsEqual(0f);

        AssertString(registry.Execute("anim on").Output).Contains("on");
        AssertFloat(clips.Weight).IsEqual(1f);
        AssertBool(registry.Execute("anim play juggle").Success).IsFalse();
    }
}
