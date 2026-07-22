# Workflow (Zip Baseline Iteration)

This project uses a **zip-baseline workflow**.

## Canonical baseline
- The source of truth for each iteration is the latest uploaded project zip named `wasteland-survivor.zip`.
- The assistant should always extract that zip first and work from the extracted project contents unless the user explicitly says to use a different baseline.

## Standard iteration loop
1. Extract the latest `wasteland-survivor.zip`.
2. Read the required project docs from the extracted workspace.
3. Implement the requested changes incrementally.
4. Update docs/versioning when the workflow or project state changes.
5. Produce a new **drop-in zip** containing the project contents, excluding:
   - `Assets/`
   - `.godot/`
   - `.git/`
   - build outputs / IDE folders such as `bin/`, `obj/`, `.vs/`
6. The user applies that zip over their local project folder while keeping local `Assets/`, `.godot/`, and `.git/`.
7. The user validates locally, then re-uploads the latest full folder again as `wasteland-survivor.zip` for the next iteration.

## Release notes / versioning expectations
Whenever a significant feature or fix is completed, explicitly update:
- `VERSION.txt`
- `CHANGELOG.md`
- any relevant docs in `Docs/`

## Assistant process notes
Keep a short living list of workflow lessons and packaging guardrails in:
- `Docs/ASSISTANT_PLAYBOOK.md`
