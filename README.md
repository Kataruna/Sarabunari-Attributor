# Attributor

Inspector attributes for Unity 6: nested groups with columns, buttons where you declare them, and a few field helpers.

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
| `Show` | `[Show] float speed;` | Shows a non-serialized field, read-only. |
| `Locked` | `[Locked] public int id;` | Greyed out. |
| `Line` | `[Line]` | Separator line above the field. |
| `EnableIf` / `DisableIf` | `[EnableIf("useCamera")]` | Editable only when / greyed out when a bool field, property or method is true. |
| `OnChange` | `[OnChange("Apply")]` | Calls a method after the field changes in the inspector. |
| `Expand` | `[Expand] public MyConfig config;` | Shows the referenced asset's fields inline. |
| `SceneName` | `[SceneName] public string scene;` | Pick a scene from Build Settings. |

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

- `Locked`, `EnableIf`, `DisableIf`, `OnChange`, `Group`, `Fold` and `Button` work on members of a `MonoBehaviour` or `ScriptableObject`, not inside nested `[Serializable]` classes. `Line`, `SceneName` and `Expand` work anywhere.
- Scripts that use none of these attributes get Unity's normal inspector.
- Not compatible with NaughtyAttributes installed in the same project (both replace the inspector) — see [MIGRATION.md](MIGRATION.md) to switch.
