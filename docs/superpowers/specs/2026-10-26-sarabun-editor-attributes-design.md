# SarabunEditor: Inspector Attributes — Design

Date: 2026-10-26
Status: approved 2026-10-26

## Intent

A Unity Editor package of inspector attributes, in the spirit of NaughtyAttributes / Odin, built to replace NaughtyAttributes in the user's own projects (first target: CoSI-Pilot).

Why it exists — two concrete NaughtyAttributes problems the user hit:

1. **No nested groups.** In `CoSI-Pilot/Assets/Scripts/Gameplay/ModeModule.cs:214-230` there are 8 `Transform` corners (an outer and an inner rectangle). The user wanted `Border > Inner > fields`, but NaughtyAttributes only goes one level, so it became a flat `Foldout("Corners")` with a `[HorizontalLine]` hack in the middle, with the attribute repeated on every field.
2. **Things get pushed to the bottom.** NaughtyAttributes draws grouped members and all `[Button]`s after the ungrouped fields, so the inspector order doesn't match the script order.

Plus the user's own goal: **names that are easier to understand** than NaughtyAttributes'.

Success = CoSI-Pilot runs with NaughtyAttributes removed, every current usage migrated, and `ModeModule`'s corners shown as nested boxes with corners in a 2×2 grid.

## Goals

- Nested groups via slash path **and** via stacking attributes.
- Groups can be always-open (`Group`) or collapsible (`Fold`).
- Optional column count per group — fields and buttons fill columns left-to-right, row by row.
- Every member (field, button, group) appears in the inspector **where it is declared in the script**.
- One attribute on a multi-field declaration (`public Transform a, b, c, d;`) applies to all of them (C# already does this — we just must not break it).
- Day-one attribute set covers everything CoSI-Pilot uses from NaughtyAttributes.
- Unity built-ins (`Header`, `Space`, `Tooltip`, `Range`, `TextArea`, `SerializeField`, `HideInInspector`, `FormerlySerializedAs`) keep working, including inside groups.

## Non-goals (v1)

- Odin-style serialization (dictionaries, interfaces, etc.).
- Validators (`Required`, `ValidateInput`, `MinValue`…), `ReorderableList`, tabs, `ShowIf`/`HideIf`, `Dropdown`, `Label`, `InfoBox` — not used in CoSI-Pilot. Add when needed.
- Grouping/`Button` and the field modifiers (`Locked`, `EnableIf`, `DisableIf`, `OnChange`) inside nested `[Serializable]` classes — modifiers are applied by the editor so they stack with any drawer, which limits them to top-level members in v1. (`Line`, `SceneName`, `Expand` are real drawers and do work in nested classes.)
- The "box is the rectangle" visual layout (mockup option B). Possible later as a group style.

## Anti-goals

- Learning a second syntax for buttons vs fields. Buttons use the same `Group`/`Fold` attributes as fields.
- Anything that changes the order of members compared to the script.
- Name clashes with Unity: e.g. `ReadOnly` collides with `Unity.Collections.ReadOnly` already used in `PictureQuality.cs` — hence `Locked`.

## Attribute set (decided)

| NaughtyAttributes | New | Behavior |
|---|---|---|
| `BoxGroup` | `Group(path)` / `Group(path, columns)` | Always-open titled box. |
| `Foldout` | `Fold(path)` / `Fold(path, columns)` | Same, but collapsible. |
| — | `Group(columns)` | **Untitled bare row**: no box, no title, just columns. |
| `Button` / `Button("Label")` | `Button` / `Button("Label")` | Method button, drawn where declared. |
| `ShowNonSerializedField` | `Show` | Read-only display of a non-serialized field. |
| `ReadOnly` | `Locked` | Field drawn but greyed out. |
| `HorizontalLine` | `Line` | Separator line above the member. |
| `EnableIf("cond")` / `DisableIf("cond")` | same names | Enable/disable based on a bool field, property, or method. |
| `OnValueChanged("M")` | `OnChange("M")` | Call method after value changes in inspector. |
| `Expandable` | `Expand` | ScriptableObject/Object reference shows its fields inline (foldout). |
| `Scene` | `SceneName` | `string` field becomes a popup of scenes in Build Settings. |

## Grouping rules (decided)

**Path form.** `[Group("Border/Inner", 2)]` — `/` separates nesting levels. The trailing number is the column count of the *last* level. No number → 1 column (normal one-per-row inspector).

**Stacking form.** `[Fold("Border")] [Group("Inner", 2)]` — stacked attributes nest left-to-right (outer first). Each level remembers whether it is a `Fold` or `Group`. This is how to mix collapsible and non-collapsible levels. The path form is shorthand for stacking levels of the same kind; both forms build the same tree. A stacked attribute may itself have a slash path.

**Bare row.** `[Group(3)]` (no name) = no box, no title, 3 columns. *Consecutive* members with `[Group(n)]` share one row; the first member without it ends the row. A bare row can sit inside a titled group: `[Fold("Tools")] [Group(2)]`.

**Buttons** are members like fields: they take the same `Group`/`Fold` attributes and fill columns the same way:

```csharp
[Group("Debug", 3)] [Button] void Spawn() { }
[Group("Debug", 3)] [Button] void Reset() { }
[Group("Debug", 3)] [Button] void Clear() { }
// → titled "Debug" box with [Spawn] [Reset] [Clear] on one row
```

**Order.** Every member is drawn at its declaration position. A group is drawn at the position of its first member; later members with the same group path are gathered into it (as Odin does).

**Target example** — `ModeModule.cs` corners become:

```csharp
[Fold("Border")] [Group("Outer", 2)] [SerializeField] protected Transform topLeftCorner, topRightCorner, bottomLeftCorner, bottomRightCorner;
[Fold("Border")] [Group("Inner", 2)] [SerializeField] protected Transform smallTopLeftCorner, smallTopRightCorner, smallBottomLeftCorner, smallBottomRightCorner;
```

Drawn as (mockup option A, chosen by the user):

```
▼ Border
  ┌ Outer ─────────────────────────┐
  │ Top Left        Top Right      │
  │ [obj ◎]         [obj ◎]        │
  │ Bottom Left     Bottom Right   │
  │ [obj ◎]         [obj ◎]        │
  └────────────────────────────────┘
  ┌ Inner ─ (same) ────────────────┐
```

## Constraints

- Unity **6000.3.12f1**.
- Distributed as a **UPM package installable by Git URL** (`package.json` at repo root, `Runtime/` and `Editor/` asmdefs). Runtime assembly holds only the attribute classes (must compile in player builds); everything else is editor-only.
- Must coexist with Unity built-in attributes and other plugins' property drawers already in CoSI-Pilot (Shapes, PrimeTween, DOTween/Demigiant, UniTask).
- Cannot coexist with NaughtyAttributes' own inspector — both register an editor for all objects. Migration removes NaughtyAttributes in the same step.

## Left to the builder (with my recommendations — my calls, not the user's)

- **Package name** (user decided): `com.sarabunari.attributor`.
- **Namespace** (user decided): `Sarabunari.Attributor` (sibling plugins will use `Sarabunari.<Name>`).
- **Repo** (user decided): `https://github.com/Kataruna/Sarabunari-Attributor.git` (private).
- **Declaration-order problem.** Reflection does not reliably interleave fields and methods in source order (they live in separate metadata tables). Recommended approach: every attribute in this package takes a hidden `[CallerLineNumber] int line = 0` constructor parameter; members with no package attribute (plain fields) are located via their `MetadataToken` order relative to neighbours, or by finding their declaration line in the `MonoScript` text. Base-class members come before derived-class members (`ModeModule` is a base class). Builder should pick whichever is robust and say why.
- **Stacking order on one member** (`[Fold("A")] [Group("B")]` on the same line). Relies on `GetCustomAttributes` returning declaration order (it does in practice under Mono/Roslyn). If that proves unreliable, fall back to an explicit `order` like Unity's `PropertyAttribute.order`.
- **IMGUI vs UI Toolkit.** Recommend UI Toolkit (`CreateInspectorGUI`), Unity 6's default, using `PropertyField` per serialized property so built-in drawers/decorators keep working.
- **Multi-column fields.** Label above the field (as in the mockup) since columns are narrow; 1-column groups use the normal label-left layout.
- **Conflicts on the same group.** If one member says `Fold("Border")` and another `Group("Border")`, the level is collapsible. If members disagree on column count, the largest wins. Log a warning in both cases.
- **Fold state** persisted per (type, group path) in `SessionState`.
- **Buttons:** methods with no parameters, or all-optional parameters (CoSI-Pilot's `ImageSaver.SaveImage(InputAction.CallbackContext _ = default)` must work). Invoke on all selected objects; record undo / mark dirty in edit mode. Label defaults to nicified method name.
- **`Show`:** fields only (matches current use); properties later if wanted. Refresh while in play mode.
- **`Expand`:** guard against infinite recursion; the expanded object uses this same inspector so its attributes work.
- **Tests:** EditMode tests on the pure "members → layout tree" logic (nesting, stacking, bare rows, ordering, columns). UI drawing verified by hand in CoSI-Pilot.

## Migration of CoSI-Pilot (follow-up, after the package works)

35 scripts use NaughtyAttributes. Mostly find-and-replace by the table above; collapse repeated `Foldout` lines into single `Group`/`Fold` lines on multi-field declarations; remove the `[HorizontalLine]` workaround in `ModeModule`; delete `Assets/Plugins/NaughtyAttributes`.
