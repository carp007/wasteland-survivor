# UiKit (shared UI/dialog toolkit)

This project contains **reusable UI infrastructure** intended to be portable to future Godot/C# games.

## What belongs here
- Typed node binding helpers (`SceneBinder`)
- Navigation helpers (`ScreenRouter`, `UiNav`)
- Modal/dialog infrastructure (`ModalHost`, `IModalService`, `ModalService`, `DialogCard`)

## What does NOT belong here
- Game-specific screens or flows (CityShell, Garage, Workshop, Arena view)
- Game-specific theme implementations (those live in the main game project)

## Styling
UiKit dialogs are game-agnostic:
- The game injects styling hooks via `ModalDialogStyle` when registering `IModalService`.

