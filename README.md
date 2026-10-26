# Don't Be Late

A chaotic low-poly comedy game about getting to work by 9:00. Built with **Godot 4.7 (.NET)** and **C#**.

- Design: [docs/dont-be-late-design-doc.md](docs/dont-be-late-design-doc.md)
- Systems overview: [docs/game-systems.md](docs/game-systems.md)
- Contributor and AI-assistant rules: [CLAUDE.md](CLAUDE.md)

## Getting started

```sh
git clone --recurse-submodules https://github.com/ahmetbekcan/dont-be-late.git
# or, in an existing clone:
git submodule update --init
```

Open `project.godot` in the Godot 4.7 .NET editor and build once (Build button or `dotnet build`).

Reusable systems live in the [ReusableGameSystems](https://github.com/ahmetbekcan/ReusableGameSystems) library, included as a submodule at `addons/ReusableGameSystems/`.

## Playing

Press F5 in the editor. The playable world is one morning: your flat (upstairs), the street and the bus stop.

1. You wake up at 7:30. Work starts at 9:00; buses leave the stop at 7:45, 8:00 and 8:10. The clock runs at 3x.
2. Look around for clues. Something may be different this morning: puddles outside the window, or the cat missing from its bed.
3. Do whatever you want. Nothing stops you: leave barefoot, in slippers, through the window...
4. Get on a bus.

Consequences catch up with you as the morning goes on: rain soaks you, puddles fill your sneakers, bare feet hurt on the pavement, a jump from the window leaves you limping, and the bus driver may not let you on. Miss the last bus that gets you to work by 9:00 and you're back in bed for a new morning.

| Key | Action |
| --- | --- |
| WASD / mouse | Move / look |
| Shift, Space, Ctrl | Sprint, jump, crouch |
| E, F, R | Interact (each object shows which key does what) |
| Tab or I | Inventory: see what you wear and carry, swap, take off, drop |
| Q / left click | Drop / throw a held object |
| `` ` `` or F1 | Debug console (`help`, `scenario force rain`, `restart`, `bus next`, `time 7:59`, `give boots`, `meter wetness 80`, `status`...) |
| F3 | Debug overlay |

## Content

| What | Where |
| --- | --- |
| Scenarios and the scenario pool | `content/scenarios/` |
| Items and the item catalog | `content/items/` |
| Meters and status effects | `content/meters/`, `content/status/` |
| Bus driver rules | `content/obstacles/bus_driver/` |
| The playable world | `scenes/main/morning.tscn` (instances `scenes/stages/home/` and `scenes/stages/street/`) |
| Game code | `src/` |

## Tests

Tests use GdUnit4 (C#). Set `GODOT_BIN` in `.runsettings` to your Godot .NET executable, then run:

```sh
dotnet test
dotnet test --filter "FullyQualifiedName~MorningTest"   # one test class while working
```
