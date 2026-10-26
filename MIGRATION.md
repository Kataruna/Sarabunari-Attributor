# Migrating from NaughtyAttributes

Attributor and NaughtyAttributes can't be installed in the same project: both replace the whole inspector. Switch a project in one go.

## 1. Check for attributes Attributor doesn't have yet

Run this from `Assets/` before you start. Anything it finds has no Attributor equivalent yet: remove it and handle that field by hand.

```bash
grep -rnE 'ShowIf|HideIf|ReorderableList|Dropdown|InfoBox|ValidateInput|ShowNativeProperty|Label\(|Tag\]|Layer\]|ResizableTextArea|ShowAssetPreview|ProgressBar|CurveRange|MinValue|MaxValue|AnimatorParam|SortingLayer|InputAxis|AllowNesting' --include='*.cs' .
```

## 2. Swap the packages

1. Install Attributor: Package Manager → **+** → **Install package from git URL** → `https://github.com/Kataruna/Sarabunari-Attributor.git`
2. Delete the NaughtyAttributes folder (CoSI-Pilot: `Assets/Plugins/NaughtyAttributes`).
3. The project won't compile until step 3 is done. That's expected.

## 3. Rename

| NaughtyAttributes | Attributor | Notes |
|---|---|---|
| `using NaughtyAttributes;` | `using Sarabunari.Attributor;` | |
| `[Foldout("X")]` | `[Fold("X")]` | Now appears where its first field is, not at the bottom. |
| `[BoxGroup("X")]` | `[Group("X")]` | |
| `[Button]`, `[Button("Label")]` | same | Now appears where the method is declared. |
| `[Button("Label", EButtonEnableMode.Playmode)]` | same | |
| `[MinMaxSlider(a, b)]` | same | `Vector2` fields only. |
| `[Required]`, `[Required("Message")]` | same | Object reference fields only. Shows an error box instead of NaughtyAttributes' warning. |
| `[ShowNonSerializedField]` | `[Show]` | |
| `[ReadOnly]` | `[Locked]` | Only in files that used NaughtyAttributes. `Unity.Collections.ReadOnly` (Jobs) stays as is. |
| `[HorizontalLine]` | `[Line]` | Color/thickness arguments aren't supported. |
| `[EnableIf("x")]`, `[DisableIf("x")]` | same | One condition only. |
| `[OnValueChanged("M")]` | `[OnChange("M")]` | |
| `[Expandable]` | `[Expand]` | |
| `[Scene]` | `[SceneName]` | `string` fields only. |

Run these commands from `Assets/Scripts`. They only touch files that contain `using NaughtyAttributes`, so Jobs `[ReadOnly]` elsewhere stays safe. Commit first so you can diff the result.

```bash
find . -name '*.cs' -print0 | xargs -0 grep -l 'using NaughtyAttributes' | tr '\n' '\0' | xargs -0 perl -pi -e '
  s/using NaughtyAttributes;/using Sarabunari.Attributor;/;
  s/\bFoldout\(/Fold(/g;
  s/\bBoxGroup\(/Group(/g;
  s/\bShowNonSerializedField\b/Show/g;
  s/\bReadOnly\b/Locked/g;
  s/\bHorizontalLine\b/Line/g;
  s/\bOnValueChanged\(/OnChange(/g;
  s/\bExpandable\b/Expand/g;
  s/\bScene\]/SceneName]/g;
'
```

(`perl` ships with macOS and Git Bash, and behaves the same on every platform, unlike `sed`.)

**Check before you commit:** if a file used both NaughtyAttributes and Jobs `[ReadOnly]`, the commands above renamed both. Run `git diff | grep Locked` and put back any Jobs ones.

## 4. Tidy up: use what Attributor adds

Nothing in this step is required, but it's why you switched.

**Nested groups instead of a flat foldout with a line hack.** `ModeModule.cs` before:

```csharp
[SerializeField, Foldout("Corners")] protected Transform bottomLeftCorner;
[SerializeField, Foldout("Corners")] protected Transform bottomRightCorner;
[SerializeField, Foldout("Corners")] protected Transform topLeftCorner;
[SerializeField, Foldout("Corners")] protected Transform topRightCorner;
[HorizontalLine]
[SerializeField, Foldout("Corners")] protected Transform smallBottomLeftCorner;
// ... 3 more
```

After:

```csharp
[Fold("Corners")] [Group("Outer", 2)] [SerializeField] protected Transform topLeftCorner, topRightCorner, bottomLeftCorner, bottomRightCorner;
[Fold("Corners")] [Group("Inner", 2)] [SerializeField] protected Transform smallTopLeftCorner, smallTopRightCorner, smallBottomLeftCorner, smallBottomRightCorner;
```

The field names stay the same, so the references you assigned in scenes and prefabs are kept. Declaring them in a different order is safe.

**Buttons in a row.** Debug buttons like `ScoreSummary.ShowScore` / `HideScore`:

```csharp
[Group(2)] [Button] public void ShowScore() { ... }
[Group(2)] [Button] public void HideScore() { ... }
```

## Behaviour differences to expect

- **Order follows the script.** Groups and buttons appear where they're declared. Inspectors that used to have all their buttons at the bottom will now have them spread through the fields.
- **Fold state** is remembered until Unity closes, per script and group.
- **`Locked`, `EnableIf`, `DisableIf`, `OnChange`, `Group`, `Fold`, `Button`** work on a script's own members, not inside nested `[Serializable]` classes. `Line`, `SceneName`, `Expand`, `MinMaxSlider` and `Required` work anywhere.
- **Button methods may have optional parameters.** Required parameters show a disabled button.
