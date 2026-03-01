# Wasteland Survivor (Godot 4.x .NET / C#)

Single-player top-down/isometric vehicular-combat RPG/sandbox inspired by AutoDuel.

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

## Project docs
- `Docs/AI_README.md` – canonical onboarding for new ChatGPT threads
- `Docs/MASTER_GAME_SPEC.md` – authoritative design north star
- `Docs/PROJECT_STATE.md` – canonical current state
- `Docs/CODEMAP.md` – quick “where to look” map
- `Docs/REFRACTORING_PLAN.md` – refactor opportunities + reusable component plan (proposal)
- `Docs/BUILD_RUN.md` – wiring + smoke tests
- `Docs/AI_WORKFLOW.md` – how to keep ChatGPT threads fast/clean
- `Docs/REPO_WORKFLOW.md` – zip-based iteration + packaging rules (Project source zips)
- `Docs/THREAD_HANDOFF_PROMPT.md` – copy/paste prompt for new threads
- `Docs/ASSISTANT_PLAYBOOK.md` – internal “what works / what doesn’t” notes to prevent repeated workflow dead-ends
- `Docs/Audio/AUDIO_CHECKLIST.md` – audio sourcing + organization checklist (plus license templates)
- `Docs/Assets/README.md` – optional asset setup (local paths; Assets aren’t shipped in AI zips)
- `Docs/NEXT_TASK.md` – the next small step
- `CHANGELOG.md` + `VERSION.txt` – track what changed and current build id
