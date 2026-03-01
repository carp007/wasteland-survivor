# Assistant Playbook (what works / what doesn’t)

This doc exists to prevent repeating workflow dead-ends across ChatGPT threads.

## Baseline access (canonical)
- **Source of truth:** the latest Project source zip uploaded by the user: `wasteland-survivor.zip`
- **Rule:** treat that zip as canonical; do not attempt to “pull from remote repo” unless the user explicitly asks.

### What *does* work reliably
- Unzip the Project file `wasteland-survivor.zip` and work locally from its extracted contents.

Example (assistant environment):
- Input zip path: `/mnt/data/wasteland-survivor.zip`
- Extract to: `/mnt/data/ws_work/`

## What *doesn’t* work reliably (avoid looping)
- pulling from any remote repository inside threads
- downloading repository snapshot zips inside threads
- fetching individual files from remote-hosted raw URLs inside threads

**Policy:** If any network-based approach fails once in a thread/session, stop and use the Project zip baseline instead. Do not keep retrying variations.

## Packaging rules (canonical)
Follow `Docs/REPO_WORKFLOW.md`:
- Standard project zip **excludes**: `.git/`, `.godot/`, `Assets/`
- Also exclude common build/IDE outputs if present: `bin/`, `obj/`, `.vs/`, `.idea/`

### Packaging sanity check (avoid “missing file” builds)
- If an iteration introduces **new C# files**, verify they are present in the deliverable zip.
  - A common failure mode is “updated code references a new class, but the new `.cs` file didn’t make it into the zip”,
    causing compile errors on the user side.

### Packaging command pattern
From the project root (the extracted folder), create a “drop-in” zip:

- Include everything under the project root **except** excluded folders.
- Ensure paths are preserved (so the user can unzip over their project folder).

## Versioning / release notes
Whenever delivering a zip:
- Bump `VERSION.txt` build id
- Add a short entry to `CHANGELOG.md`
- Update workflow/docs when process changes

## If assets are required
Our standard AI-delivered project zip excludes `Assets/`.
If a change requires new/changed files under `Assets/`:
- List the required paths explicitly in the response, and
- Prefer a **separate small asset-bundle zip** containing only those files.

## Audio work
- Canonical audio checklist: `Docs/Audio/AUDIO_CHECKLIST.md`
- Licensing manifest: `Docs/Audio/ATTRIBUTION_AND_LICENSES.md`

## Godot 4 quirks (avoid repeat bugs)
- **Autoload service registry lifecycle:** The `App` autoload owns `App.Services`. If any code replaces the
  `GameServices` instance at runtime (e.g., `Services = new GameServices()` during boot), it will silently
  discard UI service registrations added by `AppRoot` (router/modals/navigation) and break menu navigation.
  Always keep the same `GameServices` instance and only add/overwrite individual registrations.
- **Calling engine methods by string** (`CallDeferred`, `Call`, UndoRedo, etc.): use the engine's **snake_case** name (e.g., `"move_to_front"`), not the C# PascalCase wrapper.
- **Spatial shader world position**: Godot 4 spatial shaders do **not** provide `WORLD_POSITION`. Reconstruct fragment world position from view-space `VERTEX` using the built-in `INV_VIEW_MATRIX` (preferred) to avoid needing custom uniforms.
- **.tscn Node3D transforms**: when adding simple “anchor” helpers in scenes, prefer `position = Vector3(x, y, z)` (and `rotation` / `scale` if needed) over serializing a full `Transform3D(...)`. In some cases the full transform serialization can trigger a parse error on load.


## Multi-project setup (UiKit)
- The solution now includes a shared UI/dialog toolkit project: `UiKit/WastelandSurvivor.UiKit.csproj`.
- The main game project references it via `<ProjectReference>` in `Wasteland Survivor.csproj`.
- **Important:** the main csproj explicitly excludes `UiKit/**` from compilation to avoid duplicate type definitions.
  If you add new files to UiKit, make sure they live under `UiKit/` and that `UiKit/**` is still excluded from the main project.

