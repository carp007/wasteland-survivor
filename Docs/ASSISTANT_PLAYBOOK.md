# Assistant Playbook (what works / what doesn’t)

This doc is for preventing repeated workflow dead-ends across ChatGPT threads.

## Repo access (baseline)
- Source of truth is GitHub `main`: `https://github.com/carp007/wasteland-survivor.git`
- In this environment, direct `git clone` / `curl` may fail due to DNS/network restrictions.

### What *does* work reliably here
- Fetch individual files via **raw GitHub** URLs:
  - `https://raw.githubusercontent.com/carp007/wasteland-survivor/main/<path>`
- Use those raw URLs to download baseline files that need modification.

### When a full repo snapshot is required
If we truly need the entire repository contents (beyond a small patch):
- Prefer that the **user supplies a zip** (export from GitHub or `git archive`) as the iteration baseline.
- Then the assistant can apply changes locally and deliver the standard “project zip” (excluding `.git/`, `.godot/`, `Assets/`).

## Packaging rules (canonical)
Follow `Docs/REPO_WORKFLOW.md`:
- Standard project zip **excludes**: `.git/`, `.godot/`, `Assets/`
- If we must add files under `Assets/` (including `Assets/Audio/`), provide a **separate small asset bundle zip** with only the needed paths.

## Versioning / release notes
Whenever delivering a zip:
- Bump `VERSION.txt` build id
- Add a short entry to `CHANGELOG.md`
- Update docs as needed (especially workflow docs)

## Audio work
- Canonical audio checklist: `Docs/Audio/AUDIO_CHECKLIST.md`
- Licensing manifest: `Docs/Audio/ATTRIBUTION_AND_LICENSES.md`
- Keep engines as “hero sounds”: spend budget there if we spend anywhere.
