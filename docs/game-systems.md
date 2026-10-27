# Don't Be Late — Game Systems

Oct 23, 2026 · companion to `dont-be-late-design-doc.md`

The core of this game is a data-driven scenario engine, and almost every piece of it generalizes beyond this game. Systems are split into a **reusable library** (its own repo, shared across games) and a **game-specific layer** (this game's repo).

## Reusable library (separate repo)

### Core foundations

- **Event bus:** a global signal hub so systems talk without knowing about each other (e.g. `bus_missed` is heard by the clock, the consequence system and the HUD).
- **Seeded RNG service:** one seed per run, with named sub-streams, so any morning can be replayed exactly for debugging.
- **Run manager:** start run, fail run, reset to start, run counter. Generic for any "die and retry" game.
- **Stage flow / scene manager:** an ordered sequence of stages (Home → Street → Bus → Office) with transitions, loading and checkpoints.
- **Game clock:** in-game time with an adjustable time scale (1:1 or compressed), pause, scheduled callbacks at specific times ("at 8:05, do X") and deadline checks (9:00).
- **Save system:** versioned save files with a pluggable serializer, so each system saves its own data.

### World state and logic

- **Blackboard / world state:** key-value facts like `has_umbrella`, `bus_card_balance`, `is_soaked`. Everything else reads from and writes to it.
- **Conditions and requirements:** data-driven condition resources (has item, flag set, time before X, meter above Y) combinable with AND/OR/NOT. Decides whether the player met today's requirement.
- **Weighted content pool:** picks entries by weight, tags, exclusions, prerequisites and difficulty level, and supports drawing several that stack. Used for scenarios; reusable for loot, events or encounters.
- **Delayed consequences:** register "if condition is still unmet at time T or location L, fire effect E". This is the "skip the umbrella at 7:40, slip at 8:05" system.
- **Cross-run memory with decay:** facts that carry into later runs with a probability that fades each run (the shot neighbor, the angry tourist).
- **Heat / alert level:** accumulating chaos that escalates world responses (police patrols, the chasing kid). Works like a wanted level.
- **Journal / codex:** unlockable entries with discovery state; drives the fridge of sticky notes.
- **City generator:** a low-poly town from a style and a seed: streets, kerbs, crossings, traffic lights, lots with buildings and parks, props and required landmarks. Alongside the geometry it produces walk graphs (pavements, crossings tied to signals, doors, seats), car lanes and signal timing for NPCs and traffic, and a 2D map view.

### Player and actors

- **3D character controller:** movement, sprint, jump, crouch/crawl, climb, carry, built as composable components.
- **Interaction system:** interactable component with a focus raycast, prompts, and multiple verbs per object (the cat on your shoes: bribe, fight, ignore).
- **Inventory and equipment:** items as resources, stackable items, slots (shoes, tie, umbrella), and item properties like a bus card's balance.
- **Stats and meters:** a generic meter component (nausea, eyes-open, stamina) with thresholds that emit signals.
- **Status effects:** timed or conditional buffs and debuffs (coffee speed boost, sleepy, soaked) that modify stats.
- **NPC framework:** state machine or behavior tree, perception (sight and hearing), and common behaviors such as patrol, chase, block path and wait for the player.
- **Rideables / vehicles:** a generic "mount this" component with configurable steering and speed (vendor cart with no steering, scooter, bike, bus roof).
- **Ragdoll and physics reactions:** switching between animated and ragdoll, knockback, and getting back up.

### Presentation

- **Dialogue and barks:** a simple dialogue tree plus ambient one-liners, with conditions read from the blackboard (the neighbor, the radio, the roommate's note).
- **Mini-game framework:** a shared interface (start, score, finish) so wake-up variants plug in and output a result — here, the starting time.
- **Audio manager:** music, SFX pools, 3D positional cues (sirens in the distance), ducking.
- **Camera effects:** shake, blur, vignette for heavy eyelids, nausea and impacts.
- **UI kit:** HUD clock, interaction prompts, toast notifications, fail screen, pause menu.
- **Settings and input remapping**, plus **localization** from day one.

### Tooling

- **Debug console and overlay:** force a scenario, set the time, give an item, show blackboard values. Essential with random stacking scenarios.
- **Run log:** records what was drawn and what happened in each run, for reproducing bugs and tuning balance.

## Game-specific layer (Don't Be Late repo)

Built on the library, kept in the game repo:

- **Scenario definitions:** the clue → meaning → requirement entries, each a data resource using the pool, conditions and consequences.
- **Obstacles:** the puzzle boxes, built from interactables, NPCs and rideables.
- **Requirement chains:** no bus card → no balance → kiosk only takes cash.
- **The four stages** and their scenes.
- **The town:** a generated city (library `CityGen`) with the game's lot uses, props and landmarks (home, bus stop, photo studio, bank, cafe, barbershop, pharmacy, school, hospital). Shops have awnings, named signs and props; the school and hospital stand behind fenced yards on merged lots. It's new every run or one kept city, and the phone's Map app shows it with walking routes.
- **Wake-up mini games:** snooze fight, heavy eyelids, dream escape.
- **Clue presentation:** radio, window puddles, sticky notes, phone alerts.
- **Carryover content:** the specific return variants (shotgun neighbor, police tourist, kid with friends).
- **Scoring:** a chaos or style rating, if added.

## Repo setup

- The game is written in **C#** on **Godot 4 (.NET)**.
- The library is a C# Godot addon (`addons/<lib_name>/`) in its own repo, pulled into the game as a **git submodule**. Its code lives in its own namespace and compiles into the game's assembly.
- Tests use **GdUnit4** (C#).
- Changes Claude Code makes to library systems while building the game are committed in the library repo.
- A `CLAUDE.md` in the game repo tells Claude Code where code belongs and how to extend the library.

## First playable (build order)

1. Event bus, seeded RNG, run manager, game clock
2. Blackboard, conditions, content pool, delayed consequences
3. Character controller, interaction, inventory
4. Debug console and overlay
5. One stage (Home) with two scenarios, fully playable end to end
