# AI Workflow (How to keep threads fast + consistent)

## Canonical sources
1) `Docs/MASTER_GAME_SPEC.md` — authoritative design north star (mirrors the longer “Master Game Design Prompt”).
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

## After each successful build/feature in a thread
The AI will reliably update these automatically after making changes:
- `VERSION.txt` (increment build id)
- `CHANGELOG.md` (1 short entry)
- `Docs/NEXT_TASK.md` (and other docs as needed)

This makes new threads resilient even if chat context is slow/limited.

## Canonical project-access process (current)

**Baseline source of truth:** the latest uploaded project zip named `wasteland-survivor.zip`.

- **Start each iteration from:** the extracted contents of the latest uploaded zip, unless the user explicitly says to use a different baseline.
- **Workflow:** extract the zip, make the requested code/doc changes in that workspace, then return a drop-in zip that excludes `Assets/`, `.godot/`, `.git/`, and generated build / IDE folders. The user applies that zip over the local project folder, validates locally in Godot, and then re-uploads the latest full folder again as `wasteland-survivor.zip` for the next iteration.

## Assistant “what works” notes
To avoid repeating the same packaging / environment dead-ends across iterations, the assistant maintains a small internal playbook:
- `Docs/ASSISTANT_PLAYBOOK.md`

If something fails (build assumptions, missing plugins, runtime errors), the assistant should update that playbook at the end of the iteration so the next iteration doesn’t repeat the same dead-ends.

## Engineering expectations
- Use proper OOP design and follow SOLID principles when adding or refactoring code. Prefer small focused helpers/services over screen-level god classes.

## What to include in every assistant response (contract)
- What changed / propose
- Files touched
- How to verify quickly
- Next small step
