# AI Workflow (How to keep threads fast + consistent)

## Canonical sources
1) **Master Game Design Prompt** (project file) — authoritative design north star.
2) `Docs/PROJECT_STATE.md` — canonical “where we are today”.
3) `CHANGELOG.md` + `VERSION.txt` — what changed and what build is current.

## Best practice: one thread = one small objective
Create new threads for:
- a refactor chunk (e.g., “encounter finalization standardization”)
- a UI chunk (e.g., “health bars + target indicator”)
- a mechanics chunk (e.g., “towing prototype v0”)

Avoid mixing multiple objectives in one thread.

## When starting a new thread
Paste the content from `Docs/THREAD_HANDOFF_PROMPT.md`.

## After each successful build in a thread
The AI will update these automatically whenever it generates a new project zip:
- `VERSION.txt` (increment build id)
- `CHANGELOG.md` (1 short entry)
- `Docs/NEXT_TASK.md` (and other docs as needed)

This makes new threads resilient even if chat context is slow/limited.

## Zip handling
- **Process going forward:** before each iteration, the user will upload the *current* project zip into the ChatGPT Project files. Treat that uploaded zip as the authoritative working baseline for the next change.
- **Zip contents (size control):** when generating a downloadable zip, **do not include**:
  - `.godot/`
  - `Assets/`
- Assume the user keeps their existing `Assets/` folder locally. If a change requires a *new* asset file under `Assets/`, call it out explicitly in the changelog/notes so it can be copied in separately.
- When applying an AI-provided zip locally, unzip it **over** the existing project folder so `Assets/` remains intact.


## What to include in every assistant response (contract)
- What changed / propose
- Files touched
- How to verify quickly
- Next small step
