# CLAUDE.md — Don't Be Late

A chaotic low-poly comedy game about getting to work by 9:00, built in **Godot 4 (.NET)** with **C#**. Design: `docs/dont-be-late-design-doc.md`. Systems overview: `docs/game-systems.md`.

## Two repositories

This project uses two repos:

- **Game repo (this one):** content and game-specific logic for Don't Be Late.
- **Library repo (`ReusableGameSystems`):** reusable systems shared across games, included here as a git submodule at `addons/ReusableGameSystems/`.

The library's C# files live under `addons/ReusableGameSystems/` and compile into the game's assembly, so the game's `.csproj` needs no extra project reference. The library must never reference `DontBeLate.*` namespaces.

Before writing any new system, check whether the library already has it.

## Where code belongs

Put it in the **library** if it would make sense in a different game with no changes to its meaning: clocks, run loops, blackboards, conditions, weighted pools, consequences, inventory, interaction, meters, status effects, NPC behaviors, rideables, dialogue, mini-game framework, audio, camera, UI kit, save, debug tools.

Put it in the **game** if it names something from this game: buses, neighbors, cats, umbrellas, the four stages, specific scenarios and obstacles, wake-up mini games, carryover variants, scoring.

When unsure, split it: a generic mechanism in the library, the specific use in the game.

## Project layout

```
addons/ReusableGameSystems/  library (git submodule): one folder per system, namespace ReusableGameSystems.<Folder>
src/<Area>/                  game C# code, namespace DontBeLate.<Area>
                             (Scenarios, Obstacles, Requirements, Stages, WakeUp, Clues, Carryover, Scoring, DebugCommands)
content/                     data resources (.tres): scenarios, obstacles, items, conditions, consequences, carryover, dialogue, journal
scenes/                      main, stages/{home,street,bus,office}, wake_up, ui, props, characters
assets/                      models, materials, textures, audio/{music,sfx}, fonts
localization/                translation files
tests/                       GdUnit4 (C#) tests for game code, namespace DontBeLate.Tests.<Area>
docs/                        design doc and systems overview
```

C# folders use `PascalCase` to match namespaces; scene and asset folders and files use `snake_case` (Godot convention).

## Extending the library

If a library system is missing something this game needs, **improve the library itself** instead of working around it in the game. Rules:

1. **No game-specific code in the library.** No references to buses, cats, 9:00, or any Don't Be Late content. Use generic names and make values configurable.
2. **Extend, don't fork.** Prefer `[Export]` properties, `Resource` subclasses, `virtual`/`abstract` members, interfaces and signals so games customize behavior without editing library code.
3. **Keep it backward compatible.** Don't break existing public APIs; if a change is unavoidable, note it in the library's `CHANGELOG.md`.
4. **Ship examples and tests.** Every library system has a small example scene in `addons/ReusableGameSystems/examples/` and tests (GdUnit4, C#) in `addons/ReusableGameSystems/tests/` covering its public API. Update both when you change a system.
5. **Document it.** Each system has a short README: purpose, how to use it, how to extend it.
6. **Commit separately.** Commit library changes inside the submodule (library repo) first with a clear message, then commit the updated submodule pointer in the game repo.

## Architecture conventions

- **Data-driven:** scenarios, obstacles, items, conditions and consequences are `Resource` files (`.tres`), not hardcoded logic.
- **Composition over inheritance:** systems are nodes/components added to scenes (e.g. `Interactable`, `Meter`, `Rideable`).
- **Loose coupling:** systems communicate through the event bus and signals, not direct references.
- **Autoloads only for true globals:** event bus, clock, run manager, RNG, save. Everything else is a node or resource. Autoloads are scenes in `scenes/autoload/` (so their exports can be tuned) named `Events`, `Rng`, `Runs`, `Clock`; an autoload name can't match its `[GlobalClass]` name. From C#, use the class's static `Instance` (e.g. `GameClock.Instance`).
- **Determinism:** all randomness goes through the seeded RNG service so any run can be replayed from its seed.
- **Input actions** (in `project.godot`): `move_forward/back/left/right`, `sprint`, `jump`, `crouch`, `interact`, `interact_alt`, `interact_alt2`, `drop`, `throw`. Use these names; add new actions in the editor's Input Map rather than hardcoding keys.
- **Player:** start from the library's `Characters/FirstPersonPlayer.tscn` (motor, camera, interactor, abilities, inventory) instead of building a new controller.
- **Custom resources and nodes:** mark reusable `Resource` and `Node` classes with `[GlobalClass]` so they appear in the editor's Create dialog and inspector.

## Code style

- Follow the Godot C# style guide and standard .NET conventions: `PascalCase` for types, methods, properties, signals and constants; `_camelCase` for private fields; `camelCase` for locals and parameters.
- One public class per file; the file name matches the class name (required for Godot to find the script).
- The library uses its own root namespace (e.g. `ReusableGameSystems.Clock`, `ReusableGameSystems.Conditions`); the game uses `DontBeLate.*`. Namespaces replace class-name prefixes. Don't name a class the same as its namespace.
- Enable nullable reference types (`<Nullable>enable</Nullable>`) and treat warnings seriously.
- Declare signals with `[Signal] public delegate void ...EventHandler(...)` when they need to be visible to the editor or GDScript; plain C# `event`s are fine for code-only communication.
- Avoid per-frame allocations in `_Process`/`_PhysicsProcess` (LINQ, closures, boxing) in hot paths.
- Keep classes small and focused; one responsibility per class.

## Scenarios and stages

- **What a scenario is:** a `ScenarioDefinition` resource (`src/Scenarios/`, data in `content/scenarios/`). It is a pool entry (weight, tags, exclusions, difficulty) with:
  - a clue, a meaning and a requirement (text, for the journal and debug);
  - `Facts` set when the morning starts;
  - `Consequences` registered when the morning starts.
- **Clues and obstacles are not scripted per scenario.** Level objects react to facts:
  - a `ConditionalActivator` shows a prop only when a fact is set;
  - interaction verbs use conditions (e.g. the shoes can't be taken while `cat_on_shoes`).
- **Requirements are delayed consequences.** They are checked at a time or a location (`front_door`), not when the player makes the mistake.
- **To add a scenario:**
  1. Create the `.tres` and add it to `content/scenarios/home_pool.tres`.
  2. Add the props that react to its facts.
  3. Add a test in `tests/Stages/` that plays it through.
- **Morning flow:** `MorningDirector` (in each stage scene) runs it. It starts the run, resets clock, facts and consequences, draws scenarios (`ScenarioPlanner`), handles the front door and the end screens. Continuing reloads the scene for the next morning.
- **Rules every morning has** (the 8:00 bus) live in `content/scenarios/base_morning.tres`.

## Debugging

The debug console and overlay must be able to force any scenario, set the clock, give items and show blackboard values. When adding a new scenario or system, add the matching debug commands.

- The `Debug` autoload (`scenes/autoload/debug.tscn`) holds the library's `DebugConsole` (`` ` `` or F1) and `DebugOverlay` (F3). Both are stripped from release builds.
- Standard commands already cover time, runs/seed, facts, items, consequences and events (`help` in the console). Add new items to `content/items/item_catalog.tres` so `give` can find them.
- Register game commands with `DebugConsole.Instance?.Commands.Register(...)` and watches with `DebugOverlay.Instance?.Watch(...)`. Put them in `src/DebugCommands/`.
- Scenario commands: `scenario` (active), `scenario list`, `scenario force <id>` (next morning), `scenario clear`, `restart`.

## Workflow

- Build toward the first playable: event bus, RNG, run manager, clock → blackboard, conditions, content pool, delayed consequences → controller, interaction, inventory → debug console → Home stage with two scenarios.
- Run tests before committing: `dotnet test` (uses `.runsettings`; set `GODOT_BIN` there to your Godot .NET executable).
- After cloning, run `git submodule update --init` to fetch the library.
- When the design doc leaves something open (run length, time scale, between-run unlocks), make it configurable rather than hardcoding a choice.
