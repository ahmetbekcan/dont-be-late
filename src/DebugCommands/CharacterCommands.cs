using System.Collections.Generic;
using System.Text;
using Godot;
using ReusableGameSystems.Appearance;
using ReusableGameSystems.DebugTools;

namespace DontBeLate.DebugCommands;

/// <summary>Console commands for characters: list the random NPCs, give them new looks.</summary>
public static class CharacterCommands
{
    private static ulong _rerolls = 1;

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
        registry.Register("creator", "Opens the character creator for the player's look (saved on Done).", _ =>
        {
            var creator = DontBeLate.Characters.PlayerCreator.Current ?? throw new CommandException("No character creator in this scene.");
            creator.Open();
            return "creator open (close the console to use it)";
        });

        registry.Register("npcs", "Lists the random NPCs, or gives them all new looks.", args =>
        {
            if (Engine.GetMainLoop() is not SceneTree tree)
            {
                throw new CommandException("No scene.");
            }

            var randoms = new List<RandomLook>();
            Collect(tree.Root, randoms);
            if (randoms.Count == 0)
            {
                return "no random NPCs here";
            }

            if (args.GetString(0, "list") == "reroll")
            {
                foreach (var random in randoms)
                {
                    random.Apply(_rerolls++ * 7919);
                }

                return $"rerolled {randoms.Count}";
            }

            var text = new StringBuilder();
            foreach (var random in randoms)
            {
                var look = random.Look;
                text.Append(random.GetParent()?.GetParent()?.Name ?? random.Name).Append(": ")
                    .Append(look?.Body?.Id ?? "-");
                if (look != null)
                {
                    foreach (var (slot, part) in look.Parts)
                    {
                        text.Append(", ").Append(slot).Append('=').Append(part.Id);
                    }
                }

                text.Append('\n');
            }

            return text.ToString().TrimEnd();
        }, "npcs [list | reroll]");
    }

    private static void Collect(Node node, List<RandomLook> into)
    {
        if (node is RandomLook random)
        {
            into.Add(random);
        }

        for (int i = 0; i < node.GetChildCount(); i++)
        {
            Collect(node.GetChild(i), into);
        }
    }
}
