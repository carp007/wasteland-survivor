# Boot splash sequence

On launch, the game shows a sequence of splash images before entering the main UI.

## Config

Edit:

`Data/Config/boot_splash.json`

Supported fields:
- `defaultSeconds` (number) — default display duration per item.
- `fadeInSeconds` (number) — fade-in duration.
- `fadeOutSeconds` (number) — fade-out duration.
- `gapSeconds` (number) — optional **black gap** between items (after fade-out, before next fade-in).
- `background` (string) — background color (`"#RRGGBB"` or `"#RRGGBBAA"`).
- `defaultOpenSound` (string) — optional AudioStream path played when each item begins (e.g. WAV/OGG). Can be overridden per item.
- `items[]` — ordered list of images:
  - `path` (string) — Texture2D path.
  - `seconds` (number, optional) — overrides `defaultSeconds`.
  - `openSound` (string, optional) — overrides `defaultOpenSound` for this item.

Example:

```json
{
  "defaultSeconds": 2.5,
  "fadeInSeconds": 0.2,
  "fadeOutSeconds": 0.2,
  "gapSeconds": 0.12,
  "background": "#000000",
  "defaultOpenSound": "res://Assets/Audio/UI/splash_whoosh.wav",
  "items": [
    { "path": "res://Assets/Images/studio.png", "seconds": 2.0 },
    { "path": "res://Assets/Images/title.png", "seconds": 2.5, "openSound": "" }
  ]
}
```

If `seconds` is omitted per item, `defaultSeconds` is used.

## Skip

Press **Enter** or **Escape** to skip the entire splash sequence.
