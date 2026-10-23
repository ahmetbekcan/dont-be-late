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

Press F5 in the editor. The first playable is the **Home** stage:

1. You wake up at 7:30 and the bus leaves at 8:00. The clock runs at 3x, so that's 10 real minutes.
2. Look around for clues. Something may be different this morning: puddles outside the window, or the cat missing from its bed.
3. Get ready: put on shoes, take what you need.
4. Leave through the front door.

If you missed what the clue meant, it catches up with you at the door. Fail and you're back in bed for a new morning.

| Key | Action |
| --- | --- |
| WASD / mouse | Move / look |
| Shift, Space, Ctrl | Sprint, jump, crouch |
| E, F, R | Interact (each object shows which key does what) |
| Q / left click | Drop / throw a held object |
| `` ` `` or F1 | Debug console (`help`, `scenario force rain`, `restart`, `time 7:59`, `give boots`...) |
| F3 | Debug overlay |

## Content

| What | Where |
| --- | --- |
| Scenarios and the scenario pool | `content/scenarios/` |
| Items and the item catalog | `content/items/` |
| Home stage | `scenes/stages/home/home.tscn` |
| Game code | `src/` |

## Tests

Tests use GdUnit4 (C#). Set `GODOT_BIN` in `.runsettings` to your Godot .NET executable, then run:

```sh
dotnet test
```
