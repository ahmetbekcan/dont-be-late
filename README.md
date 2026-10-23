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

## Tests

Tests use GdUnit4 (C#). Set `GODOT_BIN` in `.runsettings` to your Godot .NET executable, then run:

```sh
dotnet test
```
