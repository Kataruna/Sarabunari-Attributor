# Attributor

Inspector attributes for Unity 6: nested groups with columns, buttons where you declare them, and a few field helpers.

**[Manual with examples →](https://kataruna.github.io/Sarabunari-Attributor/)**

## Install

Package Manager → **+** → **Install package from git URL** →

```
https://github.com/Kataruna/Sarabunari-Attributor.git
```

```csharp
using Sarabunari.Attributor;
```

## Attributes

| Attribute | Example | What it does |
|---|---|---|
| `Group` | `[Group("Border/Inner", 2)]` | Always-open titled box. `/` nests, the number sets columns. |
| `Group` (bare) | `[Group(3)]` | Row with no box or title. Consecutive members share it. |
| `Fold` | `[Fold("Events")]` | Same as `Group`, but collapsible. |
| `Button` | `[Button]`, `[Button("Label")]` | Method button, drawn where it is declared. Optional parameters are allowed. |
| `Button` (mode) | `[Button("Fire", EButtonEnableMode.Playmode)]` | Clickable only in Play mode (`Playmode`) or only outside it (`Editor`). |
| `Show` | `[Show] private float speed;` | Makes a private / non-serialized field readable in the inspector, not editable. |
| `ReadOnly` | `[ReadOnly] public int id;` | Locks a public / serialized field in the inspector while still showing it. |
| `Line` | `[Line]` | Separator line above the field. |
| `EnableIf` / `DisableIf` | `[EnableIf("useCamera")]` | Editable only when / greyed out when a bool field, property or method is true. |
| `OnChange` | `[OnChange("Apply")]` | Calls a method after the field's value changes while its inspector is open — edits, Undo, and code writing the field in play mode. |
| `Expandable` | `[Expandable] public MyConfig config;` | Inline arrow on a `ScriptableObject` field to expand and edit its fields right in the inspector. |
| `SceneName` | `[SceneName] public string scene;` | Pick a scene from Build Settings. |
| `MinMaxSlider` | `[MinMaxSlider(0, 10)] public Vector2 range;` | Two-handle slider plus number boxes, `x` = min, `y` = max. |
| `Required` | `[Required("Assign the canvas")] public CanvasGroup canvas;` | Error box under the field while it is empty. |
| `SelfAssign` | `[SelfAssign] public Rigidbody rb;` | Inline `Self` button that assigns `GetComponent` from the same `GameObject`. |
| `GetChild` | `[GetChild] public List<Collider> hits;` | Inline `Get Child` button that populates a list/array with matching components from child `GameObject`s. |

Stack levels to mix collapsible and open boxes — attributes nest left to right:

```csharp
[Fold("Border")] [Group("Outer", 2)] [SerializeField] Transform topLeft, topRight, bottomLeft, bottomRight;
[Fold("Border")] [Group("Inner", 2)] [SerializeField] Transform innerTopLeft, innerTopRight, innerBottomLeft, innerBottomRight;

[Group(3)] [Button] void Spawn() { }
[Group(3)] [Button] void Reset() { }
[Group(3)] [Button] void Clear() { }
```

Gives a collapsible **Border** with two 2×2 boxes, then `[Spawn] [Reset] [Clear]` on one row.

## Notes

- `ReadOnly`, `EnableIf`, `DisableIf`, `OnChange`, `GetChild`, `Group`, `Fold` and `Button` work on members of a `MonoBehaviour` or `ScriptableObject`, not inside nested `[Serializable]` classes. `Line`, `SceneName`, `Expandable`, `MinMaxSlider`, `Required` and `SelfAssign` work anywhere.
- Scripts that use none of these attributes get Unity's normal inspector.
- Not compatible with NaughtyAttributes installed in the same project (both replace the inspector) — see [MIGRATION.md](MIGRATION.md) to switch.
