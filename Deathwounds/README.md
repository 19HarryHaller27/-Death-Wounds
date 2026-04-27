# Death Wounds

| | |
|-|-|
| **Mod id** | `deathwounds` |
| **Version** | 1.0.0 |
| **Game** | Vintage Story 1.20.0+ |
| **.NET** | 10+ (`net10.0`) |

Survival: **death-touched injuries** (random leg slow or torso fast-hunger tier on death), poultice cures by body part. **Character** dialog has a **Death Wounds** panel. Standalone.

## Build

- Source project: `dotnet/DeathWounds/DeathWounds.csproj` (also references `VintagestoryLib.dll` next to the API in your game install).
- Set **`Directory.Build.props`** in **this** folder root (one file applies to the nested project) or use `-p:VintageStoryPath=...` / `VINTAGE_STORY_PATH`.

```text
dotnet build "dotnet\DeathWounds\DeathWounds.csproj" -c Release
```

Copy `dist/` layout or the built `DeathWounds.dll` + `modinfo` + `assets` into a `deathwounds` mod folder under VintagestoryData `Mods` as you already do for releases.

## Layout

- `dotnet/DeathWounds/` — C#.
- `assets/`, `modinfo.json` — runtime package; `dist/` is a prebuilt tree if present.

## License

[MIT](LICENSE)

**Author:** adams.
