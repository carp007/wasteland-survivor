# AI Readme (canonical onboarding for new ChatGPT threads)

This project is **Wasteland Survivor**.

## 1) North star (authoritative design)
Read this first:
- `Docs/MASTER_GAME_SPEC.md`

That document is the source of truth even if early prototypes simplify or stub systems.

## 2) Current implementation state
Then read:
- `Docs/PROJECT_STATE.md`
- `Docs/CODEMAP.md` (where to look in the code)

This is the canonical “where we are today” snapshot.

## 3) How to build/run & quick smoke tests
Then read:
- `Docs/BUILD_RUN.md`

## 4) How we work (rules for the AI + thread handoffs)
Then read:
- `Docs/AI_WORKFLOW.md`
- `Docs/REPO_WORKFLOW.md`
- `Docs/THREAD_HANDOFF_PROMPT.md`
- `Docs/ASSISTANT_PLAYBOOK.md` (internal “what works / what doesn’t” notes)
- `Docs/Audio/AUDIO_CHECKLIST.md` (audio checklist + licensing templates)
- `Docs/REFRACTORING_PLAN.md` (proposal: refactor opportunities + reusable components)
- `Docs/REFRACTORING_PROGRESS.md` (completed refactor steps; each ships as a zip)
- `Docs/Assets/README.md` (optional asset setup paths; Assets are not shipped in AI zips)

## 5) What to do next
Finally read:
- `Docs/NEXT_TASK.md`

## Working rules (summary)
- Keep changes incremental and compile-safe.
- UI/dialog framework code lives in the `UiKit/` project; game-specific UI screens live under `Scenes/UI` + `Scripts/UI`.
- Prefer simple, explicit code over clever abstractions.
- Save/state changes must be backward compatible (or include a small migration).
- Turn-based arena is deprecated/removed. Don’t reintroduce it.
- The project is now uses a **3D world with a fixed top-down/RTS camera** (2.5D). Arena entry point is `Scenes/UI/ArenaRealtimeView.tscn` and world is `Scenes/Arena/ArenaWorld.tscn`. Legacy 2D arena has been removed.
- Packaging rule: never include/compile `.godot/`. When generating downloadable zips, also exclude `Assets/` and `.git/` to keep downloads small (assume the user keeps Assets locally).

## Zip / versioning policy
Every time the AI generates a new project zip, it must also update:
- `VERSION.txt` (bump build id)
- `CHANGELOG.md` (add a short entry)
- `Docs/NEXT_TASK.md` (and other docs as needed)
