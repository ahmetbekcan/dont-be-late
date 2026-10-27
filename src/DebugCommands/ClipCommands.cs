using System.Collections.Generic;
using Godot;
using ReusableGameSystems.Appearance;
using ReusableGameSystems.DebugTools;

namespace DontBeLate.DebugCommands;

/// <summary>Console commands for animation clips on characters: switch them off to compare, or play one on the nearest person.</summary>
public static class ClipCommands
{
    private static bool _off;
    private static bool _watching;

    /// <summary>Registers the commands. Safe to call again.</summary>
    public static void Register()
    {
        if (DebugConsole.Instance is { } console)
        {
            Register(console.Commands);
        }
    }

    public static void Register(CommandRegistry registry)
    {
        registry.Register("anim", "Animation clips: on/off for everyone (off = the old procedural motion), list, or play/stop one on the nearest person.", args =>
        {
            switch (args.GetString(0, "info").ToLowerInvariant())
            {
                case "info":
                    return $"clips {(_off ? "off" : "on")} for {All().Count} characters";
                case "on":
                    return Switch(false);
                case "off":
                    return Switch(true);
                case "list":
                    return List(Nearest());
                case "play":
                {
                    string id = args.GetString(1);
                    var clips = Nearest();
                    if (!clips.Play(id))
                    {
                        throw new CommandException($"No clip \"{id}\". {List(clips)}");
                    }

                    return $"{Name(clips)}: {id}";
                }

                case "stop":
                {
                    var clips = Nearest();
                    clips.Stop();
                    return $"{Name(clips)}: stopped";
                }

                default:
                    throw new CommandException("Usage: anim [on | off | list | play <clip> | stop]");
            }
        }, "[on | off | list | play <clip> | stop]");
    }

    private static string Switch(bool off)
    {
        _off = off;
        var all = All();
        foreach (var clips in all)
        {
            clips.Weight = off ? 0 : 1;
        }

        // People made later (the crowd grows its pool as needed) follow the switch too.
        if (!_watching && Engine.GetMainLoop() is SceneTree tree)
        {
            _watching = true;
            tree.NodeAdded += node =>
            {
                if (node is CharacterClips added && _off)
                {
                    added.Weight = 0;
                }
            };
        }

        return $"clips {(off ? "off: procedural walk and idle" : "on")} for {all.Count} characters";
    }

    private static string List(CharacterClips clips)
    {
        var ids = new List<string>();
        foreach (var clip in new[] { clips.Idle, clips.Walk, clips.Run, clips.Sit })
        {
            if (clip != null && clip.Id.Length > 0)
            {
                ids.Add(clip.Id);
            }
        }

        foreach (var clip in clips.Actions)
        {
            if (clip != null && clip.Id.Length > 0)
            {
                ids.Add(clip.Id);
            }
        }

        return "clips: " + string.Join(", ", ids);
    }

    /// <summary>The visible character with clips nearest the camera.</summary>
    private static CharacterClips Nearest()
    {
        var tree = Engine.GetMainLoop() as SceneTree ?? throw new CommandException("No scene.");
        var from = tree.Root.GetCamera3D()?.GlobalPosition ?? Vector3.Zero;
        CharacterClips? best = null;
        float bestDistance = float.MaxValue;
        foreach (var clips in All())
        {
            if (clips.GetParent() is not Node3D body || !body.IsVisibleInTree())
            {
                continue;
            }

            float distance = body.GlobalPosition.DistanceSquaredTo(from);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = clips;
            }
        }

        return best ?? throw new CommandException("Nobody with animation clips here.");
    }

    private static string Name(CharacterClips clips)
    {
        var tree = Engine.GetMainLoop() as SceneTree;
        float distance = clips.GetParent() is Node3D body && tree?.Root.GetCamera3D() is { } camera
            ? body.GlobalPosition.DistanceTo(camera.GlobalPosition)
            : 0;
        return $"person {distance:0} m away";
    }

    private static List<CharacterClips> All()
    {
        var found = new List<CharacterClips>();
        if (Engine.GetMainLoop() is SceneTree tree)
        {
            Collect(tree.Root, found);
        }

        return found;
    }

    private static void Collect(Node node, List<CharacterClips> found)
    {
        if (node is CharacterClips clips)
        {
            found.Add(clips);
        }

        foreach (var child in node.GetChildren())
        {
            Collect(child, found);
        }
    }
}
