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

## After each successful build in a thread
The AI will update these automatically whenever it generates a new project zip:
- `VERSION.txt` (increment build id)
- `CHANGELOG.md` (1 short entry)
- `Docs/NEXT_TASK.md` (and other docs as needed)

This makes new threads resilient even if chat context is slow/limited.

## Project zip handling (current process)

**Baseline source of truth:** the **latest Project source zip** uploaded by the user: `wasteland-survivor.zip`.

- **Start each iteration from:** the latest `wasteland-survivor.zip` in the Project files (unless the user explicitly provides a different baseline).
- **Do not rely on remote repo pulls** in threads (they have been unreliable). Treat the Project zip as canonical.
- **AI deliverable each iteration:** a single project zip containing the whole project **excluding**:
  - `.git/`
  - `.godot/`
  - `Assets/`
- **Assumptions:** the user keeps their existing `Assets/` and `.godot/` folders locally.
  - If a change requires *new/changed* files under `Assets/`, call it out explicitly in notes (and ideally deliver it as a separate small “asset bundle” zip).

### User local apply steps (expected)
1) Delete local project contents **except** `Assets/`, `.godot/`, `.git/`.
2) Unzip the AI zip into the project folder.
3) Build/run and validate.
4) Upload the updated full folder again as `wasteland-survivor.zip` for the next iteration (even if minor bugs remain; only hold back if something is catastrophically broken).

For the full step-by-step workflow and conventions, see `Docs/REPO_WORKFLOW.md` (kept as the workflow doc name even though we’re zip-based).

## Assistant “what works” notes
To avoid repeating the same packaging / environment dead-ends across iterations, the assistant maintains a small internal playbook:
- `Docs/ASSISTANT_PLAYBOOK.md`

If something fails (zip handling, packaging exclusions, build assumptions), the assistant should update that playbook at the end of the iteration.

## What to include in every assistant response (contract)
- What changed / propose
- Files touched
- How to verify quickly
- Next small step
