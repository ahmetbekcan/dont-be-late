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
                             (Scenarios, Obstacles, Requirements, Stages, WakeUp, Clues, Carryover, Scoring, PhoneApps, Characters, City, DebugCommands)
content/                     data resources (.tres): scenarios, obstacles, items, meters, status, conditions, consequences, carryover, dialogue, journal, phone (apps, messages), city (style, lot uses, landmarks, props)
scenes/                      main (morning.tscn: the playable world), stages/{home,street,city,bus,office}, wake_up, ui, props (city: landmark scenes), characters (npc.tscn)
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
- **Input actions** (in `project.godot`): `move_forward/back/left/right`, `sprint`, `jump`, `crouch`, `interact`, `interact_alt`, `interact_alt2`, `drop`, `throw`, `inventory`. Use these names; add new actions in the editor's Input Map rather than hardcoding keys.
- **Player:** start from the library's `Characters/FirstPersonPlayer.tscn` (motor, camera, interactor, abilities, inventory, meters, status effects) instead of building a new controller.
- **Custom resources and nodes:** mark reusable `Resource` and `Node` classes with `[GlobalClass]` so they appear in the editor's Create dialog and inspector.

## UI look

- The game's look is the **toy box** style: chunky rounded panels, thick navy outlines, bright colors, the Fredoka font. It lives in one theme, `assets/ui/toy_theme.tres`, set as the project theme (`gui/theme/custom`).
- **Don't style nodes one by one.** Use the theme, and its type variations for special pieces (`HudClock`, `HudToast`, `HudPrompt`, `HudStatus`, `HudGear`, the library's `EndScreen...` variations). Add new variations to the theme for new pieces.
- **The inventory screen** is styled by an `InventorySkin` (`assets/ui/inventory_skins/*.tres`), set on `InventoryScreen.Skin` in `morning.tscn`. Its 3D figure is `scenes/ui/inventory_figure.tscn`; `EquipmentVisual` nodes there show what's worn (add one when adding a wearable item).
- **Characters** come from the library's `Appearance` system: low-poly puppets built in code. The parts kit is editable `.tres` in `content/characters/kit/` (parts, body types, `catalog.tres` with palettes and random rules); the player's look is `content/characters/looks/player.tres`; `content/characters/worn/` holds parts shown for worn and held items (shoes matching the item props, umbrella and phone in hand). The inventory figure (`scenes/ui/inventory_figure.tscn`) is a `CharacterBody` with `EquipmentParts`; when adding a wearable or holdable item, add a matching part and mapping there. Random NPCs: instance `scenes/characters/npc.tscn` (a `RandomLook` draws from the run's seeded `characters` stream, a `CharacterIdle` keeps them breathing and glancing around, a `CharacterBlink` blinks); three wait at the bus stop. The kit files were exported from the library's `StarterKit` and are now ours to edit; exporting again would overwrite them. How the kit fits the body:
  - Bodies are flexible (one skinned mesh): arms and legs bend at elbows and knees without seams and keep their thickness. Animate the joints as usual.
  - Eyes are `EyePart`s: the ball snaps into the face, the pupil lies on it and the lids wrap it (`UpperLid`, `LowerLid`, `LidTilt`), so they always fit together. The upper lids are joints of their own (`UpperLidL`/`R`); a `CharacterBlink` under the body blinks them (on the NPC, walker, inventory figure and creator; `Closed` holds them shut, e.g. asleep).
  - Face parts sit on the head's real surface (`Surface` on their shapes: brows lie along the forehead, glasses arms hinge back to the head). Set it on new face parts.
  - Haircuts are grown from the head (`HairShellPart`: hairline, volume, length, clumps, curls, parting, bald crown); buns, ponytails and spikes are shapes snapped onto the head on top. The mohawk is a snapped crest on its own.
  - Brows and mouths are `BrowPart`s and `MouthPart`s: strokes drawn on the face, so a `CharacterFace` can move them. Expressions are `FaceExpression`s in `content/characters/expressions/` (happy, angry, surprised, sad, scared, sleepy, annoyed, smug); a face has a `Mood` and `React`s for a while.
  - Animation clips: `content/characters/clips/` are `CharacterClip`s baked from the Quaternius Universal Animation Library (CC0, `assets/animations/quaternius_ual/`) by `scenes/tools/bake_clips.tscn` (list a clip there, then run `Godot --headless --path . res://scenes/tools/bake_clips.tscn`; re-baking keeps hand-tuned `Rate`/`BlendTime`). Walkers play them with `CharacterClips` in `walker.tscn`: idle, walk and jog by speed, sit on benches, `startled` when startled; the procedural `CharacterIdle`/`CharacterWalk` stay underneath (the head is left to them, so people still glance around).
  - Small clothing details are painted on (`Prints`: the V-neck, the shirt pocket). The jacket is a coat with its own layer (`Shell`). `HemAt` sets where short sleeves and shorts end. The player's look is saved by `PlayerLookStore` (`user://player_look.tres`, else the default) and edited in the library's `CharacterCreatorScreen` (`Creator` in `morning.tscn`, opened through `PlayerCreator`: the "Change look" button under the inventory figure, or the `creator` console command, or start the game with `-- --creator`; from the inventory it returns there when done). In the creator, scroll to zoom to the face and drag up and down to look along the body.
- **The phone** is the library's `PhoneScreen` (`Phone` in `morning.tscn`, the default "real phone" skin). The player starts with the `phone` item (slot 1); holding it opens the phone, cursor free, camera still, legs still walking. Apps are `.tres` in `content/phone/apps/` (Buses and Weather are game apps in `src/PhoneApps/`; Map is a "coming soon" `TextPhoneApp` until navigation exists; Messages is the library's). Messages arrive from `content/phone/messages/` via the `Messages` scheduler (clock time plus an optional condition, e.g. the rain alert only when `raining`). App icons are Noto emoji PNGs in `assets/ui/icons/phone/`.
- **The hotbar** (always visible at the bottom, `HUD/Hotbar`) is the library's `HotbarHud` over `Player/Hotbar` (`HotbarHolder`, `Size` = number of slots, 1-10). Keys 1-0 / mouse wheel take an item in hand; it stays in the bag; Q drops it (or whatever you carry). The item in hand shows at `Player/Head/HeldItem` (an item's `HeldScene`, e.g. `scenes/props/items/held/`, else its world scene's meshes). Holding is what matters for hand items: the umbrella only keeps you dry in hand (`HeldItemCondition`).
- Item icons are `ItemDefinition.Icon` PNGs in `assets/ui/icons/` (rendered from Noto Color Emoji, OFL) until there's custom art. Fonts and icons ship with their license files.
- Before building a new player-facing screen, render 2-3 mockups and let the user pick.

## Code style

- Follow the Godot C# style guide and standard .NET conventions: `PascalCase` for types, methods, properties, signals and constants; `_camelCase` for private fields; `camelCase` for locals and parameters.
- One public class per file; the file name matches the class name (required for Godot to find the script).
- The library uses its own root namespace (e.g. `ReusableGameSystems.Clock`, `ReusableGameSystems.Conditions`); the game uses `DontBeLate.*`. Namespaces replace class-name prefixes. Don't name a class the same as its namespace.
- Enable nullable reference types (`<Nullable>enable</Nullable>`) and treat warnings seriously.
- Declare signals with `[Signal] public delegate void ...EventHandler(...)` when they need to be visible to the editor or GDScript; plain C# `event`s are fine for code-only communication.
- Avoid per-frame allocations in `_Process`/`_PhysicsProcess` (LINQ, closures, boxing) in hot paths.
- Keep classes small and focused; one responsibility per class.

## Player freedom and consequences

The core rule: **never block the player; let the world react.** The player can do anything (leave barefoot, jump out of the window, ignore the rain). Mistakes aren't refused or punished on the spot. They come back later through systems that keep running.

- **No gates.** Don't put requirements on verbs that stop the player doing something sensible. A requirement may only describe what's physically possible right now (you can't "wear" shoes a cat is sitting on, but you can always "grab" them).
- **No artificial time penalties.** Don't use `AdvanceClockEffect` for mistakes. The clock just runs; consequences cost time naturally (you walk slower, you have to go back for a towel, the bus leaves without you).
- **Consequences are world reactions**, built from library systems:
  - meters (`wetness`) and status effects (`wet_socks`, `sore_feet`, `limping`) on the player;
  - `EffectZone`s for rain, puddles, pavement, shelters and indoors;
  - characters and NPCs that react (the cat, the bus driver's `BoardingRule`s).
- **The run only fails when the deadline becomes impossible** (the last bus that gets you to work by 9:00 leaves), or when you board a bus that's too late.

## Scenarios and stages

- **The world is one continuous scene:** `scenes/main/morning.tscn` instances `scenes/stages/home/home.tscn` (the flat, upstairs, with outdoor stairs) and `scenes/stages/street/street.tscn` (pavement, puddles, bus stop, buses), plus the player, HUD and `MorningDirector`. Later stages join the same way.
- **What a scenario is:** a `ScenarioDefinition` resource (`src/Scenarios/`, data in `content/scenarios/`). It is a pool entry (weight, tags, exclusions, difficulty) with:
  - a clue, a meaning and a requirement (text, for the journal and debug);
  - `Facts` set when the morning starts (e.g. `raining`, `cat_on_shoes`);
  - optional `Consequences` registered when the morning starts.
- **Scenarios only set facts.** Everything else reacts to them: zones and props switch on (`ConditionalActivator`), NPCs change behavior (the cat sits on the shoes), conditions in data change outcomes.
- **Items in the world:** each item's `WorldScene` (`scenes/props/items/<id>.tscn`) is what it becomes when dropped from the inventory screen (Tab). Those objects can be put on, put in the bag, or carried and thrown.
- **Facts the world keeps:** `indoors` (inside the flat), `under_cover` (bus shelter), plus scenario facts.
- **To add a scenario:**
  1. Create the `.tres` and add it to `content/scenarios/home_pool.tres`.
  2. Add the props, zones or NPC behavior that react to its facts, and the consequence that catches up with a player who ignored the clue.
  3. Add a test in `tests/Stages/MorningTest.cs` that plays it through.
- **Morning flow:** `MorningDirector` starts the run, resets clock, facts and consequences, draws scenarios (`ScenarioPlanner`), and ends the morning on `BusService` signals (boarded, or the last on-time bus missed). Continuing reloads the scene for the next morning.
- **Buses:** `BusService` (in the street scene) drives buses from the game clock (schedule 7:45, 8:00, 8:10; 49-minute ride; work at 9:00, all configurable). The driver checks the rules in `content/obstacles/bus_driver/`.

## The sky

- `scenes/main/sky.tscn` is the world's light: `WorldEnvironment`, `Sun`, `Moon` and the library's `SkyCycle` (`Cycle`). `morning.tscn` and `city_preview.tscn` instance it; new world scenes should too, instead of their own environment and sun.
- It follows the game clock through a full day and night. The "golden" looks are in `content/sky/` (night, predawn, dawn, morning, day, afternoon, golden_hour, dusk, twilight), and `sky_material.tres` sets the cloud, sun, moon and star style. Keyframe hours are set on `Cycle` in `sky.tscn`.
- It turns overcast while `raining` is set (`overcast.tres`, or `overcast_night.tres` in the dark).
- It publishes how dark it is as the global shader parameter `sky_night` (0 day, 1 night; declared in `project.godot`). City windows light up with it; use it for anything that should glow at night.
- Lights after dark:
  - Street lamp lenses and car head and tail lights glow, following the library's `NightGlow`. The sky sets it, counting heavy rain as a little dark, so car lights come on in the rain.
  - `Stage/StreetLights` in `city_preview.tscn` puts real spotlights under the 16 lamps nearest the camera.
  - The `Traffic` spawner shines headlight beams from cars near the camera.

## The city

The town is generated by the library's `CityGen` (streets, pavements, crossings, traffic lights, lots, buildings, parks, props, landmarks), plus walk and drive paths and a map, all from one seed.

- **Content** lives in `content/city/`:
  - `city_style.tres`: the grid, sizes, colours, furniture rules and the list of landmarks.
  - `lot_uses/`: what lots become (house, flats, shop, office, park, woods, parking), drawn from `lot_uses.tres` by weight.
  - `landmarks/`: places every city has (`home`, `bus_stop`, `photo_studio`, `bank`), with map icons in `assets/ui/icons/map/`.
  - `props/`: tree, lamp, bench, bin and car models.
  - The coast:
    - `city_style.tres` has `Coast` on: the seed picks the sea's side.
    - A beach slopes into shallow water you can wade into. `scenes/props/city/sea_zone.tscn` gives you `wet_socks` and fills `wetness` while you're in it.
    - The water is `sea.tres` (calm and glassy).
    - The mountains around town are `backdrop.tres` (tall, jagged, snowy), from the library's `Landscape`.
  - Surfaces (the "lived-in" look): `city_style.tres` `Materials` maps each surface kind (`asphalt`, `pavement`, `kerb`, `wall`, `roof`, `trim`, `window`, `door`, `grass`, `parking`, `lot`, `path`, `outskirts`) to a material in `assets/materials/city/surfaces/`. They're shaders in `assets/materials/city/shaders/` (no image files): bricks, pavers, cracked asphalt, grass, and windows with curtains, blinds and lit rooms. Tune a look by editing its `.tres`; a lot use's ground picks its kind with `GroundSurface`.
  - Pavement landmarks can use a scene, e.g. `scenes/props/city/bus_stop.tscn` (shelter with the `under_cover` zone).
- **To add a landmark:**
  1. Add a `LandmarkRule` `.tres` to `landmarks/` and list it in `city_style.tres`.
  2. Give it either a `Use` (a lot with a builder) or a `Scene` (pavement).
  3. Add it to `CityTest.EveryCityHasEveryLandmark`.
- **Seeds:** `CityStage` (`src/City/`) builds the city when the run starts and puts the player outside `home`.
  - `CitySettings.Mode` chooses `NewEachRun` (seed derived from the run seed, so a replayed run replays its city) or `Kept` (one saved city).
  - `CityStore` saves the choice to `user://city.tres`.
- **Preview:** `scenes/stages/city/city_preview.tscn` is a playable generated town with the phone. `morning.tscn` still uses the hand-built street until it moves to the generated city.
- **Phone Map** (`src/PhoneApps/MapPhoneApp.cs`): the library's `CityMapView` with your arrow, landmark pins and the walking route to a picked place. Where there's no generated city it shows a placeholder.
- **Traffic and people** come from the library's `CityLife`. Under `Stage` in the preview:
  - `Life` (`CitySimulation`) steps the simulation;
  - `Traffic` (`TrafficSpawner`) shows the cars from `content/city/cars/`;
  - `Crowd` (`CrowdSpawner`) shows people from `scenes/characters/walker.tscn` (the NPC look with a `CharacterWalk`).
- **Settings and drivers:** `content/city/traffic.tres` and `content/city/crowd.tres` set the counts. Drivers are `DriverProfile`s in `content/city/drivers/`:
  - `calm`, `normal`, `in_a_hurry` and `crazy`, mixed by weight;
  - each car rolls its own speed, drift, gaps, reaction, amber and red running, and awareness of the player.
- **People:** kinds of people are `WalkerProfile`s in `content/city/walkers/`:
  - `commuter` (quick, rarely looks up), `stroller` (slow, curious, does anything), `local` (chats and window-shops), `phone_zombie` (walks with the phone out);
  - things they do are `WalkerActivityDefinition`s in `content/city/activities/`: `check_phone` (stops at the side of the pavement), `chat` (two people stop and face each other), `window_shop` (in front of a `shop` lot);
  - each activity names a gesture in `content/characters/gestures/` (`phone` holds `worn/held_phone.tres`, `talk`, `browse`), shown by the `Gestures` node in `walker.tscn`. Add a gesture there when an activity uses a new one;
  - curious people glance at the player when near, and everyone turns to a honk or a crash (startled: a surprised face);
  - they steer round you facing where they're going; with no room they stop, face you, look annoyed, then turn back another way. Faces come from `CharacterFace` in `walker.tscn` and the `Feelings` on `Crowd` (`content/characters/expressions/`).
- **Safety:** between NPCs it's absolute. Only the player can be hit.
- **Reactions:** `TrafficReactions` (`src/City/`) answers the simulation. A honk is a toast. A hit shoves you and applies `bruised` (plus `limping` on a hard hit): slower, never a fail.
- **Paths:** `CityLayout.Walk`, `CityLayout.Lanes` (with `Conflicts` and `Crossings`) and `CityLayout.Signals` are what the simulation runs on.

## Debugging

The debug console and overlay must be able to force any scenario, set the clock, give items and show blackboard values. When adding a new scenario or system, add the matching debug commands.

- The `Debug` autoload (`scenes/autoload/debug.tscn`) holds the library's `DebugConsole` (`` ` `` or F1) and `DebugOverlay` (F3). Both are stripped from release builds.
- Standard commands already cover time, runs/seed, facts, items, consequences and events (`help` in the console). Add new items to `content/items/item_catalog.tres` so `give` can find them.
- Register game commands with `DebugConsole.Instance?.Commands.Register(...)` and watches with `DebugOverlay.Instance?.Watch(...)`. Put them in `src/DebugCommands/`.
- Scenario commands: `scenario` (active), `scenario list`, `scenario force <id>` (next morning), `scenario clear`, `restart`.
- `sky` shows the hour, overcast and sun height; `sky hour <h>` holds an hour (`sky hour clock` follows the clock again); `sky overcast <0-1 | auto>` forces the weather.
- `npcs` lists the random NPCs; `npcs reroll` gives them new looks. `creator` opens the character creator for the player.
- `anim [on | off | list | play <clip> | stop]`: animation clips on everyone, or the old procedural motion (off) to compare; play one on the nearest person.
- `phone [open | close | apps | <app>]` and `msg <from> <text>` drive the phone.
- `hold` shows the hotbar; `hold <slot | item | none>` takes something in hand.
- Bus commands: `bus` (timetable), `bus next` (jump to just before the next bus), `bus board`. The library's `meter` and `status` commands show or change the player's wetness and status effects.
- City commands: `city` (seed, mode, landmarks, which way the sea is), `city new`, `city seed <code>`, `city keep`, `city mode run|kept`, `city goto <landmark>`, `city beach` (stand on the beach), `city paths` (show walkways and lanes).
- Traffic commands:
  - `traffic [on | off | count <n> | drivers | crazy]`: `crazy` gives the nearest car a crazy driver;
  - `crowd [on | off | count <n> | kinds | why | do <activity>]`: `why` says what the nearest person is up to, `do` makes them do an activity now, `labels` shows (or hides) who everyone within 40 m is and what they're doing;
  - `signals`: the lights at the nearest junction;
  - `honk [hit [speed]]`: try the reactions.

## Workflow

- Build toward the first playable: event bus, RNG, run manager, clock → blackboard, conditions, content pool, delayed consequences → controller, interaction, inventory → debug console → Home stage with two scenarios.
- Run tests before committing: `dotnet test` (uses `.runsettings`; set `GODOT_BIN` there to your Godot .NET executable). Tests run Godot `--headless` (no window, so they never grab the mouse); remove `<Parameters>` there to watch them run.
- **Keep the test loop fast:**
  - While working, run only the tests you touched: `dotnet test --filter "FullyQualifiedName~MorningTest"` (add `--no-build` when only re-running). Before committing, run the tests that can notice the change: its system's tests plus the tests of scenes that use it (e.g. a character change: `Appearance`, plus `MorningTest`, `CityLifeTest` and `CityLifeNodesTest`, which load the NPC, walker and inventory figure). Run the full suite only for wide changes (shared systems like events, clock, conditions or the player, project settings, or a big push), not for every commit.
  - In scene tests, advance time with `runner.Step(frames)` / `StepSeconds(s)` (library `TestTime`), never `SimulateFrames(n, 16)`: it runs 8x faster than real time with the same physics step.
  - Test logic as plain C# where possible (like `BusTimetable`), and keep full-scene tests to behavior that needs the real world.
  - Move rigid bodies in tests through the physics server (see `MorningTest.Teleport`); setting `GlobalPosition` on a resting body can be overwritten.
- If `dotnet test` says "Rebuild Godot Project ends with exit code: -1073741819" and runs fewer tests than expected, Godot crashed opening the project after a C# build (it happens on the first launch after a build). Open it once (`Godot --path . -e --headless --quit-after 1000`) and run the tests again.
- After cloning, run `git submodule update --init` to fetch the library.
- When the design doc leaves something open (run length, time scale, between-run unlocks), make it configurable rather than hardcoding a choice.
