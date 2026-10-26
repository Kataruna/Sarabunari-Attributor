# Attributor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A Unity UPM package `com.sarabunari.attributor` with nestable `Group`/`Fold` layout attributes (with columns), in-place `Button`s, and the day-one attribute set that replaces NaughtyAttributes.

**Architecture:** Runtime assembly holds only attribute classes. The Editor assembly has three layers: `MemberCollector` (reflection → ordered member list, source order via `[CallerLineNumber]` and script-text lookup), `LayoutBuilder` (pure: member list → group tree, unit-tested), and `AttributorEditor`/`InspectorBuilder` (group tree → UI Toolkit elements). Per-field modifiers (`Locked`, `EnableIf`, `DisableIf`, `OnChange`) are applied by the editor, not by PropertyDrawers, so they stack with each other and with any drawer. Value drawers (`SceneName`, `Expand`) and the `Line` decorator are normal Unity drawers.

**Tech Stack:** Unity 6000.3.12f1, C#, UI Toolkit, Unity Test Framework 1.6.0 (NUnit, EditMode).

**Spec:** `docs/superpowers/specs/2026-10-26-sarabun-editor-attributes-design.md`

## Global Constraints

- Unity **6000.3.12f1**; editor binary `/Applications/Unity/Hub/Editor/6000.3.12f1/Unity.app/Contents/MacOS/Unity`.
- Package name `com.sarabunari.attributor`; namespace `Sarabunari.Attributor` (editor code: `Sarabunari.Attributor.Editor`).
- Repo `https://github.com/Kataruna/Sarabunari-Attributor.git`, repo root = package root (`package.json` at root).
- Runtime asmdef must compile in player builds (no `UnityEditor` references). Editor asmdef: Editor platform only.
- Public attribute names exactly: `Group`, `Fold`, `Button`, `Show`, `Locked`, `Line`, `EnableIf`, `DisableIf`, `OnChange`, `Expand`, `SceneName`.
- Members appear in declaration order; a group appears at its first member's position.
- Unity built-ins (`Header`, `Space`, `Tooltip`, `Range`, `TextArea`, `HideInInspector`, `FormerlySerializedAs`) keep working inside groups.

## Test harness

Packages need a host project. Task 1 creates `/Users/sarabunari/Documents/Playground/SarabunEditor/AttributorHost` (outside this repo) that references the package by `file:` path and lists it in `testables`. Every test run is:

```bash
UNITY=/Applications/Unity/Hub/Editor/6000.3.12f1/Unity.app/Contents/MacOS/Unity
"$UNITY" -batchmode -nographics -projectPath ../AttributorHost -runTests -testPlatform EditMode \
  -testResults "$PWD/../AttributorHost/results.xml" -logFile - [-testFilter <Name>]
```

Exit code `0` = all passed, `2` = test failures, anything else = compile/launch error (read the log). Close any open Editor on the host project first (batchmode can't share it). Referred to below as **RUN TESTS**.

## Review Focus

1. **A class with no Attributor attributes** must look exactly like Unity's default (UI Toolkit) inspector, and drawers in its nested `[Serializable]` classes must still render → plain path uses `InspectorElement.FillDefaultInspector` (Task 4 tests).
2. **Inherited members across files** (CoSI-Pilot's `ModeModule` is a base class): base-class members first, each class ordered from its own source file (Task 3 test).
3. **Script source not findable** (class name ≠ file name, class in a DLL) and **field name used in a method body above its declaration**: no exception; buttons fall back to the end of their class, fields keep token order (Task 3 tests).
4. **Button methods with optional parameters** (`ImageSaver.SaveImage(InputAction.CallbackContext _ = default)`) must invoke; methods with required parameters show a disabled button with a tooltip instead of throwing (Task 5 tests).
5. **Multi-object selection**: buttons invoke on every target; `EnableIf` evaluates on the first target (Task 5 test for invoke).

---

### Task 1: Package scaffold, host project, runtime attributes

**Files:**
- Create: `package.json`, `.gitignore`
- Create: `Runtime/Sarabunari.Attributor.asmdef`, `Runtime/LayoutAttributes.cs`, `Runtime/FieldAttributes.cs`
- Create: `Editor/Sarabunari.Attributor.Editor.asmdef`
- Create: `Tests/Editor/Sarabunari.Attributor.Tests.asmdef`, `Tests/Editor/AttributeTests.cs`
- Create (outside repo): `../AttributorHost/` Unity project

**Interfaces:**
- Produces (namespace `Sarabunari.Attributor`):
  - `abstract class LevelAttribute : Attribute { string Path; int Columns; int Line; abstract bool Foldable; }` — `Path == null` means bare row; `Columns == 0` means "not specified".
  - `GroupAttribute(string path, int columns = 0, [CallerLineNumber] int line = 0)`, `GroupAttribute(int columns, [CallerLineNumber] int line = 0)`; `Foldable => false`. `AttributeUsage(Field | Method, AllowMultiple = true)`.
  - `FoldAttribute(string path, int columns = 0, [CallerLineNumber] int line = 0)`; `Foldable => true`. Same usage.
  - `ButtonAttribute(string label = null, [CallerLineNumber] int line = 0)` — `string Label; int Line`. Usage `Method`.
  - `ShowAttribute([CallerLineNumber] int line = 0)` — `int Line`. Usage `Field`.
  - Plain `Attribute`s (usage `Field`): `LockedAttribute()`, `EnableIfAttribute(string condition)`, `DisableIfAttribute(string condition)` (`string Condition`), `OnChangeAttribute(string method)` (`string Method`).
  - `PropertyAttribute`s: `LineAttribute()`, `SceneNameAttribute()`, `ExpandAttribute()`.

- [ ] **Step 1: Create the host project**

```bash
"$UNITY" -batchmode -nographics -createProject ../AttributorHost -quit -logFile -
```
Then edit `../AttributorHost/Packages/manifest.json`: add `"com.sarabunari.attributor": "file:../../Attributor"` and `"com.unity.test-framework": "1.6.0"` to `dependencies`, and a top-level `"testables": ["com.sarabunari.attributor"]`.

- [ ] **Step 2: Scaffold package files**

`package.json`: `name` `com.sarabunari.attributor`, `displayName` `Attributor`, `version` `0.1.0`, `unity` `6000.3`, `author` `{ "name": "Sarabunari" }`. Asmdefs: Runtime `Sarabunari.Attributor` (rootNamespace same); Editor `Sarabunari.Attributor.Editor` (references Runtime, `includePlatforms: ["Editor"]`); Tests `Sarabunari.Attributor.Tests` (references Runtime + Editor, `UnityEngine.TestRunner`, `UnityEditor.TestRunner`, `optionalUnityReferences`/`overrideReferences` with `nunit.framework.dll`, `defineConstraints: ["UNITY_INCLUDE_TESTS"]`, Editor platform only). `.gitignore`: `*.meta` is **not** ignored (UPM packages need them) — ignore only `.DS_Store`.

- [ ] **Step 3: Write the failing test** (`Tests/Editor/AttributeTests.cs`)

```csharp
[Test] public void Group_CapturesPathColumnsAndLine() {
    var g = new GroupAttribute("Border/Inner", 2);
    Assert.AreEqual("Border/Inner", g.Path); Assert.AreEqual(2, g.Columns);
    Assert.Greater(g.Line, 0); Assert.IsFalse(g.Foldable);
}
[Test] public void BareGroup_HasNullPath() { var g = new GroupAttribute(3); Assert.IsNull(g.Path); Assert.AreEqual(3, g.Columns); }
[Test] public void Fold_IsFoldable_DefaultColumnsUnspecified() { var f = new FoldAttribute("Border"); Assert.IsTrue(f.Foldable); Assert.AreEqual(0, f.Columns); }
[Test] public void LineNumber_CapturedFromAttributeUsage() {
    var a = typeof(LineProbe).GetField("x").GetCustomAttribute<GroupAttribute>();
    Assert.AreEqual(LineProbe.ExpectedLine, a.Line);
}
// class LineProbe { public const int ExpectedLine = <line of the field below>; [Group("A")] public int x; }
```

- [ ] **Step 4: RUN TESTS** — expected: compile error (types missing), non-zero exit.
- [ ] **Step 5: Implement the attribute classes** per Interfaces.
- [ ] **Step 6: RUN TESTS** — expected: exit 0, 4 passed.
- [ ] **Step 7: Commit** (`git add -A && git commit -m "feat: package scaffold and runtime attributes"`), including `.meta` files Unity generated.

---

### Task 2: LayoutBuilder (pure group tree)

**Files:**
- Create: `Editor/Layout.cs` (model + builder)
- Test: `Tests/Editor/LayoutBuilderTests.cs`

**Interfaces:**
- Consumes: `LevelAttribute` (Task 1).
- Produces (namespace `Sarabunari.Attributor.Editor`):
  - `enum MemberKind { Field, Button, Show }`
  - `sealed class MemberEntry { string Name; MemberKind Kind; MemberInfo Info; int Line; IReadOnlyList<LevelAttribute> Levels; }` (constructor taking all five; `Info` may be null in tests).
  - `abstract class LayoutNode`; `sealed class MemberNode : LayoutNode { MemberEntry Member; }`; `sealed class GroupNode : LayoutNode { string Title /*null = bare row or root*/; string Key /*unique path key*/; bool Foldable; int Columns /*≥1*/; List<LayoutNode> Children; }`
  - `static GroupNode LayoutBuilder.Build(IReadOnlyList<MemberEntry> orderedMembers, List<string> warnings)` — root has `Title == null`, `Key == ""`, `Columns == 1`.

- [ ] **Step 1: Write failing tests** — helper `M(name, params LevelAttribute[] levels)` builds a `MemberEntry`.

```csharp
[Test] public void Ungrouped_KeepOrder()            // a, b → root children [a, b]
[Test] public void SlashPath_Nests()                 // [Group("Border/Inner",2)] a → root/Border(Columns 1)/Inner(Columns 2)/a
[Test] public void Stacking_EqualsPath_MixedFoldable() // [Fold("Border")][Group("Inner",2)] → Border.Foldable==true, Inner.Foldable==false, Inner.Columns==2
[Test] public void Group_AppearsAtFirstMember_GathersLater() // a[G("X")], b, c[G("X")] → root [X{a,c}, b]
[Test] public void SiblingGroups_ShareParent()       // [Fold("Border")][Group("Outer",2)] o1..o4, [Fold("Border")][Group("Inner",2)] i1..i4 → root [Border{Outer{o1..o4}, Inner{i1..i4}}]
[Test] public void BareRow_ConsecutiveShareRow_BrokenByUngrouped() // [G(3)] a,b, c, [G(3)] d → root [row{a,b}, c, row{d}], rows Title==null, Columns==3, distinct Keys
[Test] public void BareRow_InsideTitledGroup()       // [Fold("Tools")][Group(2)] a,b → Tools{row{a,b}}
[Test] public void FoldAndGroup_SamePath_Foldable_Warns() // a[Fold("X")], b[Group("X")] → X.Foldable==true, warnings.Count==1
[Test] public void ColumnConflict_LargestWins_Warns() // a[G("X",2)], b[G("X",3)] → X.Columns==3, warnings.Count==1
[Test] public void UnspecifiedColumns_DoNotConflict() // a[G("X",2)], b[G("X")] → Columns==2, no warnings
```

- [ ] **Step 2: RUN TESTS** (`-testFilter LayoutBuilderTests`) — expected: compile error.
- [ ] **Step 3: Implement `LayoutBuilder.Build`** in `Editor/Layout.cs`. Algorithm:
  1. For each member, expand `Levels` (in attribute order) into segments `(title, foldable, columns)`: split `Path` on `/`; `Columns` attaches to the last segment of that attribute, earlier segments get 0; `Path == null` → one bare segment `(null, false, Columns)`.
  2. Walk segments from root, building a key `parentKey + "/" + title`. A bare segment's key is `parentKey + "/#row" + n`, where the same `n` is reused only if the **previous member** also had a bare segment under the same `parentKey`; otherwise a new `n`.
  3. Look up or create the `GroupNode` by key in a dictionary; on creation, append it to its parent's `Children` (this gives "first member position"). Merge `Foldable` with OR and `Columns` with max over non-zero values; add a warning (`"Attributor: group '<key>' declared as both Fold and Group"` / `"... has conflicting column counts"`) on the first conflict per key.
  4. Append a `MemberNode` to the deepest group (or root). Finally normalise `Columns == 0` → 1.
- [ ] **Step 4: RUN TESTS** — expected: exit 0, 10 passed.
- [ ] **Step 5: Commit** — `feat: layout tree builder`.

---

### Task 3: MemberCollector (reflection + declaration order)

**Files:**
- Create: `Editor/MemberCollector.cs`
- Test: `Tests/Editor/MemberCollectorTests.cs`, fixtures `Tests/Editor/Fixtures/OrderFixture.cs`, `Tests/Editor/Fixtures/BaseFixture.cs`, `Tests/Editor/Fixtures/DerivedFixture.cs`, `Tests/Editor/Fixtures/MismatchedFile.cs` (contains class `NotMatchingName : MonoBehaviour`)

**Interfaces:**
- Consumes: `MemberEntry`, `MemberKind` (Task 2); attributes (Task 1).
- Produces: `static List<MemberEntry> MemberCollector.Collect(Type type, Func<Type, string> sourceOf = null)` — `sourceOf` returns a class's script text or null; default implementation finds the `MonoScript` whose `GetClass() == type` via `AssetDatabase.FindAssets("t:MonoScript " + type.Name)`. Also `static bool MemberCollector.HasAnyAttributorAttribute(Type type)` (any member in the hierarchy carries a `LevelAttribute`, `ButtonAttribute`, `ShowAttribute`, `LockedAttribute`, `EnableIf`/`DisableIf` or `OnChange`). Both methods cache results in a static `Dictionary<Type, …>` (cleared by domain reload) — the editor is catch-all, so they run on every selection.

Rules:
- Walk the hierarchy **base-first**, stopping before any type whose namespace starts with `UnityEngine`/`UnityEditor`.
- Per class (`DeclaredOnly`, instance, public + non-public): all fields → `Field` kind, or `Show` kind if it has `[Show]`; methods with `[Button]` → `Button` kind. `Levels` = the member's `LevelAttribute`s in `GetCustomAttributes` order.
- Fields keep `MetadataToken` order. Field line = first Task 1 attribute's `Line` if any, else the line of the first regex match of `\b<name>\b\s*[;,=]` in the source at or after the previous field's line (for `<X>k__BackingField`, search `X`), else inherited from the previous field (`-1` if none). `// ponytail: regex scan, a Roslyn parse if it ever misplaces members`.
- Each button goes before the first field of its class whose line > button line; buttons with `Line == 0` or classes whose source is null keep their fields in token order and buttons go after them (sorted by line, then token).

- [ ] **Step 1: Write failing tests**

```csharp
[Test] public void ButtonBetweenFields_StaysInPlace()      // OrderFixture: int a; [Button] void B(){}; int c; → [a, B, c]
[Test] public void ShowField_HasShowKind()                 // OrderFixture: [Show] float s; → Kind == Show
[Test] public void BaseMembersFirst_EachFromOwnFile()      // DerivedFixture : BaseFixture → base [b1, BaseButton, b2] then derived [d1, DerivedButton]
[Test] public void NameUsedInMethodAboveDeclaration_OrderStillCorrect() // OrderFixture has a method above `late` that assigns `late = 1;` and a button declared after `late`
[Test] public void SourceMissing_ButtonsAtEndOfClass_NoThrow() // Collect(typeof(NotMatchingName)) and Collect(typeof(OrderFixture), _ => null) → fields in token order, buttons last
[Test] public void StackedLevels_KeepAttributeOrder()      // [Fold("A")][Group("B")] x → Levels[0] is FoldAttribute, Levels[1] is GroupAttribute
[Test] public void HasAnyAttributorAttribute_FalseForPlainClass()
```

- [ ] **Step 2: RUN TESTS** — expected: compile error.
- [ ] **Step 3: Implement `MemberCollector`** per Rules.
- [ ] **Step 4: RUN TESTS** — expected: exit 0, 7 passed.
- [ ] **Step 5: Commit** — `feat: member collector with source order`.

---

### Task 4: AttributorEditor — groups, columns, fields

**Files:**
- Create: `Editor/AttributorEditor.cs`, `Editor/InspectorBuilder.cs`, `Editor/Attributor.uss`
- Test: `Tests/Editor/InspectorBuilderTests.cs`, fixtures `Tests/Editor/Fixtures/BorderFixture.cs` (mirrors CoSI-Pilot `ModeModule` corners: `[Fold("Border")][Group("Outer",2)] public Transform tl, tr, bl, br;` and `Inner` likewise, plus a `[Header("H")] public int afterGroup;` and `[HideInInspector] public int hidden;` inside `Group("Border/Outer")`), `Tests/Editor/Fixtures/PlainFixture.cs` (no Attributor attributes on its own members; holds a nested `[Serializable] class Inner { [SceneName] public string scene; }` field — `PlainClass_NestedSceneName_Renders` needs Task 6, so mark it `[Ignore("Task 6")]` until then)

**Interfaces:**
- Consumes: `MemberCollector.Collect`, `HasAnyAttributorAttribute`, `LayoutBuilder.Build`.
- Produces:
  - `[CustomEditor(typeof(UnityEngine.Object), true), CanEditMultipleObjects] public class AttributorEditor : UnityEditor.Editor` — `CreateInspectorGUI()` — when `!HasAnyAttributorAttribute(target.GetType())`, creates `root`, calls `InspectorElement.FillDefaultInspector(root, serializedObject, this)` and returns it (never `null`: that would drop to IMGUI, where `CreatePropertyGUI`-only drawers show "No GUI Implemented"); otherwise builds, logs `warnings` via `Debug.LogWarning`, returns the root.
  - `static VisualElement InspectorBuilder.Build(SerializedObject so, GroupNode root)`.
  - `static VisualElement InspectorBuilder.CreateMember(SerializedObject so, MemberEntry m)` — hook point extended in Task 5; in this task: `Field` → `PropertyField(so.FindProperty(m.Name))` named `m.Name`; returns `null` when the property is null or the field has `[HideInInspector]`.
  - Element contract (tests rely on it): titled `Group` → `VisualElement` class `attributor-box` with a `Label` class `attributor-title` and content; `Fold` → `Foldout` class `attributor-fold`, `text = Title`, open state read/written with `SessionState.GetBool/SetBool(type.FullName + key, true)`; bare row → `VisualElement` class `attributor-row`. Every group element is named its `Key`. A group with `Columns > 1` puts children in a container class `attributor-columns` (`flex-direction: row; flex-wrap: wrap`), each child with `style.width = Length.Percent(100f / Columns)` and class `attributor-cell`.
  - `Attributor.uss` (loaded from `Packages/com.sarabunari.attributor/Editor/Attributor.uss`): box border/padding/margin like the Unity HelpBox; inside `.attributor-cell`, `.unity-base-field` uses `flex-direction: column` and `.unity-base-field__label` has `min-width: 0` (label above field).
  - The disabled `m_Script` field is drawn first, as Unity's default inspector does.

- [ ] **Step 1: Write failing tests** (create a `GameObject` with the fixture, `Editor.CreateEditor(component)`, call `CreateInspectorGUI()`, query with `UQuery`; destroy objects in `TearDown`)

```csharp
[Test] public void PlainClass_DefaultInspector()                  // PlainFixture → non-null, one PropertyField per serialized field, no attributor-* classes
[Test] public void PlainClass_NestedSceneName_Renders()           // PlainFixture has a nested [Serializable] with [SceneName] string → after rootVisualElement.Bind(so), a PopupField<string> exists
[Test] public void Border_IsFoldout_ContainsOuterThenInnerBoxes()  // Q<Foldout>("/Border").text=="Border"; its attributor-box children named "/Border/Outer", "/Border/Inner" in that order
[Test] public void Outer_HasTwoColumns_FourFieldsInOrder()        // "/Border/Outer" has attributor-columns with 4 attributor-cell; PropertyFields named tl,tr,bl,br
[Test] public void HiddenField_NotDrawn()                         // Q<PropertyField>("hidden") == null
[Test] public void UngroupedAfterGroup_StaysAfter()               // root child order: m_Script, "/Border", afterGroup
```

- [ ] **Step 2: RUN TESTS** — expected: compile error.
- [ ] **Step 3: Implement `AttributorEditor`, `InspectorBuilder`, `Attributor.uss`** per Interfaces.
- [ ] **Step 4: RUN TESTS** — expected: exit 0, 5 passed.
- [ ] **Step 5: Manual check** — open `AttributorHost` in the Editor, add `BorderFixture` to a GameObject: Border folds and remembers state after reselecting; Outer/Inner show 2×2 with labels above slots; `Header("H")` renders. Note results in the commit message.
- [ ] **Step 6: Commit** — `feat: inspector with nested groups and columns`.

---

### Task 5: Buttons, Show, and field modifiers

**Files:**
- Create: `Editor/MemberElements.cs`
- Modify: `Editor/InspectorBuilder.cs` (`CreateMember` dispatches to `MemberElements`)
- Test: `Tests/Editor/MemberElementsTests.cs`, fixture `Tests/Editor/Fixtures/ActionsFixture.cs`

**Interfaces:**
- Consumes: `InspectorBuilder.CreateMember`, `MemberEntry`.
- Produces (static class `MemberElements`):
  - `VisualElement CreateButton(SerializedObject so, MethodInfo method, ButtonAttribute attr)` — `Button` named method name, text = `attr.Label ?? ObjectNames.NicifyVariableName(method.Name)`. If any parameter lacks a default: `SetEnabled(false)`, tooltip `"Attributor: Button methods need no parameters or only optional ones"`.
  - `void Invoke(MethodInfo method, UnityEngine.Object[] targets)` — per target: `Undo.RecordObject`, invoke with `Type.Missing` for each optional parameter, `EditorUtility.SetDirty`; when not playing and the target is a `Component`, `EditorSceneManager.MarkSceneDirty(scene)`.
  - `VisualElement CreateShow(SerializedObject so, FieldInfo field)` — read-only display (`TextField`/`Label` with `ToString()`), disabled, refreshed with `schedule.Execute(...).Every(250)`.
  - `bool EvaluateCondition(object target, string name)` — bool field, bool property, or parameterless bool method (any visibility, searched up the hierarchy); unknown name → `false` plus one `Debug.LogWarning`.
  - `void ApplyModifiers(VisualElement element, SerializedObject so, FieldInfo field)` — `[Locked]` → `SetEnabled(false)`; `[EnableIf]`/`[DisableIf]` → re-evaluated on `targetObject` every 100 ms (`// ponytail: polling, switch to change events if inspectors get heavy`); `[OnChange("M")]` → `PropertyField.RegisterValueChangeCallback` invoking `M` on every target, skipping the first callback fired on bind.

- [ ] **Step 1: Write failing tests**

```csharp
[Test] public void Invoke_RunsOnAllTargets()             // two ActionsFixture components, Invoke(Increment) → both counters == 1
[Test] public void Invoke_OptionalParameterMethod_Runs()  // ActionsFixture.Save(int x = 5) sets last = x → last == 5
[Test] public void RequiredParameterMethod_ButtonDisabled() // CreateButton for Needs(int x) → !enabledSelf
[Test] public void ButtonLabel_DefaultsToNicifiedName()  // "Increment Counter" for IncrementCounter; [Button("Go")] → "Go"
[Test] public void Condition_FieldPropertyMethod()        // EvaluateCondition for bool field, bool property, bool method → expected values
[Test] public void ButtonsInBareRow_ThreeCells()          // ActionsFixture: [Group(3)][Button] A,B,C → one attributor-row with attributor-columns, 3 Buttons in order
[Test] public void Locked_DisablesField()                 // [Locked] public int locked → PropertyField("locked") !enabledSelf
```

- [ ] **Step 2: RUN TESTS** — expected: compile error.
- [ ] **Step 3: Implement `MemberElements`** and dispatch from `CreateMember` (`Button` → `CreateButton`, `Show` → `CreateShow`, `Field` → existing `PropertyField` + `ApplyModifiers`).
- [ ] **Step 4: RUN TESTS** — expected: exit 0, all passed.
- [ ] **Step 5: Manual check** in host: `EnableIf` toggles live; `OnChange` fires once per edit; button in a scene object marks scene dirty and Undo reverts it.
- [ ] **Step 6: Commit** — `feat: buttons, show, and field modifiers`.

---

### Task 6: Line, SceneName, Expand drawers

**Files:**
- Create: `Editor/Drawers.cs`
- Test: `Tests/Editor/DrawerTests.cs`, fixture `Tests/Editor/Fixtures/DrawerFixture.cs`, `Tests/Editor/Fixtures/SelfRefAsset.cs` (`ScriptableObject` with `[Expand] public SelfRefAsset other;`)

**Interfaces:**
- Produces:
  - `[CustomPropertyDrawer(typeof(LineAttribute))] LineDrawer : DecoratorDrawer` — `CreatePropertyGUI` returns a 1px-high element class `attributor-line` with vertical margin.
  - `[CustomPropertyDrawer(typeof(SceneNameAttribute))] SceneNameDrawer : PropertyDrawer` — `string` fields only (others: `HelpBox` "SceneName works on string fields"). `PopupField<string>` of `SceneNameDrawer.Options()`; a current value not in the list is shown as `"<value> (missing)"`.
  - `static List<string> SceneNameDrawer.Options()` — `Path.GetFileNameWithoutExtension` of enabled `EditorBuildSettings.scenes`.
  - `[CustomPropertyDrawer(typeof(ExpandAttribute))] ExpandDrawer : PropertyDrawer` — `PropertyField` for the reference plus a `Foldout` holding `new InspectorElement(obj)` when non-null (so the target's own Attributor layout applies); does not expand when the referenced object is `property.serializedObject.targetObject` or already being expanded higher in the chain (static stack of objects being built), or deeper than 3 levels.

- [ ] **Step 1: Write failing tests**

```csharp
[Test] public void SceneOptions_MatchEnabledBuildScenes()  // set EditorBuildSettings.scenes to two test entries (one disabled), restore in TearDown → one name
[Test] public void Expand_SelfReference_DoesNotRecurse()  // SelfRefAsset a; a.other = a; build inspector → returns without hanging and contains no InspectorElement for `a` itself
[Test] public void Line_AndRange_BothApply()              // DrawerFixture: [Line][Range(0,1)] public float f → field still a Slider and an attributor-line element exists before it after binding (bind via a temporary EditorWindow or `rootVisualElement.Bind(so)`)
```

- [ ] **Step 2: RUN TESTS** — expected: compile error.
- [ ] **Step 3: Implement the three drawers**, and remove the `[Ignore("Task 6")]` from `PlainClass_NestedSceneName_Renders`.
- [ ] **Step 4: RUN TESTS** — expected: exit 0, all passed.
- [ ] **Step 5: Commit** — `feat: Line, SceneName, Expand drawers`.

---

### Task 7: Release v0.1.0

**Files:**
- Create: `README.md` — install line (`Package Manager → + → Install package from git URL → https://github.com/Kataruna/Sarabunari-Attributor.git`), one table of attributes with a one-line example each (from the spec table), the `ModeModule` corners example.

- [ ] **Step 1: Full RUN TESTS** — exit 0.
- [ ] **Step 2: Install-from-URL check** — in a scratch copy of the host manifest, replace the `file:` reference with the Git URL (after pushing) and confirm the package resolves and compiles (`-batchmode -quit`, exit 0).
- [ ] **Step 3: Commit, tag, push** — `git commit -m "docs: README"`, `git tag v0.1.0`, `git push --follow-tags`.

CoSI-Pilot migration is the spec's follow-up and gets its own plan after this ships.
