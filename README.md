# Wasteland Survivor – Feature Thread 01 (Godot 4.x .NET / C#)

This zip contains the folder structure, C# code, sample JSON definitions, and a minimal scene for Feature Thread 01.

## What to copy
Copy the folders `Scenes/`, `Scripts/`, and `Data/` into the root of your Godot project (so they become `res://Scenes`, `res://Scripts`, `res://Data`).

## Godot wiring (minimal clicks)
1. Project Settings → **Autoload**
   - Add `res://Scripts/App/App.cs`
   - Name: `App`
   - Enable

2. Set main scene
   - Project Settings → **Run** → Main Scene: `res://Scenes/Main.tscn`

3. Run
   - You should see a small overlay in the top-left with version + loaded counts.
   - Output console prints a boot summary and any warnings/errors from definition loading.

Notes:
- If you already have a Main scene, you can just use the scripts + Data and recreate the node tree manually.
- The loader reads JSON from: `res://Data/Defs/**`

## Project docs (new)
- `Docs/AI_README.md` – canonical onboarding for new ChatGPT threads
- `Docs/MASTER_GAME_SPEC.md` – authoritative design north star
- `Docs/PROJECT_STATE.md` – canonical current state
- `Docs/BUILD_RUN.md` – wiring + smoke tests
- `Docs/AI_WORKFLOW.md` – how to keep ChatGPT threads fast/clean
- `Docs/THREAD_HANDOFF_PROMPT.md` – copy/paste prompt for new threads
- `Docs/NEXT_TASK.md` – the next small step
- `CHANGELOG.md` + `VERSION.txt` – track what changed and current build id
