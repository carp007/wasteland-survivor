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

## Repo + zip handling (current process)
**Baseline source of truth:** the public GitHub repo, branch `main`.

- **Start each iteration from:** `main` (latest HEAD) unless the user provides a specific commit SHA/tag.
- **AI deliverable each iteration:** a single project zip containing the whole project **excluding**:
  - `.git/`
  - `.godot/`
  - `Assets/`
- **Assumptions:** the user keeps their existing `Assets/` and `.godot/` folders locally.
  - If a change requires *new/changed* files under `Assets/`, call it out explicitly in notes (and ideally keep it as a separate small asset bundle).
- **User local apply steps (expected):**
  1) Delete local project contents **except** `Assets/`, `.godot/`, `.git/`.
  2) Unzip the AI zip into the project folder.
  3) Build/run and validate.
  4) Push the updated code to `main` even if minor bugs remain (unless something is massively broken).

For the full step-by-step workflow and conventions, see `Docs/REPO_WORKFLOW.md`.

## Assistant “what works” notes
To avoid repeating the same repo/packaging dead-ends across iterations, the assistant maintains a small internal playbook:
- `Docs/ASSISTANT_PLAYBOOK.md`

If something fails (repo access method, packaging, build assumptions), the assistant should update that playbook at the end of the iteration.


## What to include in every assistant response (contract)
- What changed / propose
- Files touched
- How to verify quickly
- Next small step
