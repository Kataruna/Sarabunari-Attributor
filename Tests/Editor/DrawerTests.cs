using System.Collections;
using System.Linq;
using NUnit.Framework;
using Sarabunari.Attributor.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Sarabunari.Attributor.Tests
{
    public class DrawerTests
    {
        public class HostWindow : EditorWindow { }

        HostWindow window;
        UnityEditor.Editor editor;
        EditorBuildSettingsScene[] savedScenes;
        readonly System.Collections.Generic.List<Object> created = new System.Collections.Generic.List<Object>();

        [SetUp]
        public void SetUp() => savedScenes = EditorBuildSettings.scenes;

        [TearDown]
        public void TearDown()
        {
            EditorBuildSettings.scenes = savedScenes;
            if (editor != null) Object.DestroyImmediate(editor);
            if (window != null) window.Close();
            foreach (var o in created) if (o != null) Object.DestroyImmediate(o);
            created.Clear();
        }

        /// <summary>Puts the target's inspector in a real window and binds it, like the Inspector does.</summary>
        IEnumerator Show(Object target)
        {
            window = ScriptableObject.CreateInstance<HostWindow>();
            window.Show();
            editor = UnityEditor.Editor.CreateEditor(target);
            var root = editor.CreateInspectorGUI();
            window.rootVisualElement.Add(root);
            root.Bind(editor.serializedObject);
            for (var i = 0; i < 5; i++) yield return null;
        }

        T Component<T>() where T : Component
        {
            var go = new GameObject("test");
            created.Add(go);
            return go.AddComponent<T>();
        }

        [UnityTest]
        public IEnumerator BareRowOfThreeButtons_StaysOnOneLine()
        {
            yield return Show(Component<ActionsFixture>());
            window.position = new Rect(100, 100, 420, 600);
            for (var i = 0; i < 5; i++) yield return null;
            var row = window.rootVisualElement.Q(className: "attributor-row").Query<Button>().ToList();
            Assert.AreEqual(3, row.Count);
            Assert.AreEqual(row[0].worldBound.y, row[2].worldBound.y, 0.5f);
        }

        [UnityTest]
        public IEnumerator UnknownCondition_WarnsOnce()
        {
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("doesNotExist"));
            yield return Show(Component<TypoFixture>());
            var until = EditorApplication.timeSinceStartup + 0.5;
            while (EditorApplication.timeSinceStartup < until) yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void UnityBaseClassFields_StillDrawn()
        {
            var c = Component<UnityBaseFixture>();
            editor = UnityEditor.Editor.CreateEditor(c);
            var root = editor.CreateInspectorGUI();
            CollectionAssert.AreEqual(new[] { "m_Script", "baseField", "own", "Rebuild" }, root.Children().Select(e => e.name));
        }

        [Test]
        public void SceneOptions_MatchEnabledBuildScenes()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene("Assets/Scenes/One.unity", true),
                new EditorBuildSettingsScene("Assets/Two.unity", false),
            };
            CollectionAssert.AreEqual(new[] { "One" }, SceneNameDrawer.Options());
        }

        [UnityTest]
        public IEnumerator PlainClass_NestedSceneName_Renders()
        {
            yield return Show(Component<PlainFixture>());
            // Unity builds a nested class's fields when its foldout opens.
            window.rootVisualElement.Query<Foldout>().ForEach(f => f.value = true);
            for (var i = 0; i < 5; i++) yield return null;
            Assert.IsNotNull(window.rootVisualElement.Q<PopupField<string>>());
        }

        [UnityTest]
        public IEnumerator Line_AndRange_BothApply_AndSeparatorAliasDrawsLine()
        {
            yield return Show(Component<DrawerFixture>());
            Assert.IsNotNull(window.rootVisualElement.Q<Slider>());
            Assert.AreEqual(2, window.rootVisualElement.Query(className: "attributor-line").ToList().Count);
        }

        [UnityTest]
        public IEnumerator MinMaxSlider_ShowsRange_AndClampsTypedValue()
        {
            var c = Component<DrawerFixture>();
            yield return Show(c);
            Assert.AreEqual(new Vector2(2, 8), window.rootVisualElement.Q<MinMaxSlider>().value);
            window.rootVisualElement.Q<FloatField>("y").value = 50;
            for (var i = 0; i < 3; i++) yield return null;
            editor.serializedObject.Update();
            Assert.AreEqual(10, editor.serializedObject.FindProperty("range").vector2Value.y);
        }

        [UnityTest]
        public IEnumerator Required_WarnsOnlyWhileEmpty()
        {
            var c = Component<DrawerFixture>();
            yield return Show(c);
            var warning = window.rootVisualElement.Q<HelpBox>(className: "attributor-required");
            Assert.AreEqual("Canvas missing", warning.text);
            Assert.AreEqual(DisplayStyle.Flex, warning.resolvedStyle.display);

            editor.serializedObject.FindProperty("canvas").objectReferenceValue = c.gameObject.AddComponent<CanvasGroup>();
            editor.serializedObject.ApplyModifiedProperties();
            var until = EditorApplication.timeSinceStartup + 0.5;
            while (EditorApplication.timeSinceStartup < until) yield return null;
            Assert.AreEqual(DisplayStyle.None, warning.resolvedStyle.display);
        }

        [UnityTest]
        public IEnumerator Expandable_InlineArrow_AndInlineEdit_ModifiesScriptableObject()
        {
            var c = Component<DrawerFixture>();
            yield return Show(c);

            var foldout = window.rootVisualElement.Q<Foldout>(className: "attributor-expand");
            var toggle = foldout.Q<Toggle>(className: Foldout.toggleUssClassName);
            Assert.AreEqual(DisplayStyle.None, toggle.resolvedStyle.display);

            var asset = ScriptableObject.CreateInstance<SelfRefAsset>();
            created.Add(asset);
            editor.serializedObject.FindProperty("config").objectReferenceValue = asset;
            editor.serializedObject.ApplyModifiedProperties();
            var until = EditorApplication.timeSinceStartup + 0.3;
            while (EditorApplication.timeSinceStartup < until) yield return null;

            // Arrow is now visible and inline on the left of the field row.
            Assert.AreEqual(DisplayStyle.Flex, toggle.resolvedStyle.display);
            var objField = foldout.Q<ObjectField>();
            Assert.Less(toggle.worldBound.x, objField.worldBound.x);
            Assert.AreEqual(toggle.worldBound.center.y, objField.worldBound.center.y, 4f);

            // Expanding shows the ScriptableObject's fields and edits apply directly to the asset.
            foldout.value = true;
            for (var i = 0; i < 5; i++) yield return null;
            var powerField = foldout.Q<IntegerField>();
            Assert.IsNotNull(powerField);
            Assert.AreEqual(10, powerField.value);
            powerField.value = 42;
            for (var i = 0; i < 3; i++) yield return null;
            Assert.AreEqual(42, asset.power);
        }

        [UnityTest]
        public IEnumerator Expand_SelfReference_DoesNotRecurse()
        {
            var a = ScriptableObject.CreateInstance<SelfRefAsset>();
            var b = ScriptableObject.CreateInstance<SelfRefAsset>();
            created.Add(a);
            created.Add(b);
            a.other = b;
            b.other = a;
            yield return Show(a);

            // Keep opening every expand foldout; a chain a -> b -> a must stop.
            for (var round = 0; round < 6; round++)
            {
                foreach (var f in window.rootVisualElement.Query<Foldout>(className: "attributor-expand").ToList())
                    f.value = true;
                for (var i = 0; i < 3; i++) yield return null;
            }

            Assert.LessOrEqual(window.rootVisualElement.Query<InspectorElement>().ToList().Count, 3);
            Assert.IsNotNull(window.rootVisualElement.Q(className: "attributor-expand-skip"));
        }

        [UnityTest]
        public IEnumerator SelfAssign_AssignsComponentWhenPresent_AndShowsRedErrorWithWarningWhenMissing()
        {
            var c = Component<DrawerFixture>();
            yield return Show(c);

            var button = window.rootVisualElement.Q<Button>(className: "attributor-self-assign");
            var error = window.rootVisualElement.Q<Label>(className: "attributor-self-assign-error");
            Assert.IsNotNull(button);
            Assert.AreEqual(DisplayStyle.None, error.resolvedStyle.display);

            // Button lives in the header row: the list keeps the full width and the size field stays clear of it.
            var root = window.rootVisualElement.Q("childSpheres");
            Assert.AreEqual(root.layout.width, root.Q<ListView>().layout.width, 0.5f);
            var size = root.Q(className: ListView.arraySizeFieldUssClassName);
            Assert.LessOrEqual(size.worldBound.xMax, button.worldBound.xMin);
            Assert.Less(button.worldBound.yMin, size.worldBound.yMax);

            // Missing component: warns, shows red error text, and leaves field null.
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("BoxCollider"));
            using (var e = NavigationSubmitEvent.GetPooled())
            {
                e.target = button;
                button.SendEvent(e);
            }
            for (var i = 0; i < 3; i++) yield return null;
            editor.serializedObject.Update();
            Assert.IsNull(c.box);
            Assert.AreEqual(DisplayStyle.Flex, error.resolvedStyle.display);
            StringAssert.Contains("BoxCollider", error.text);

            // Add component and click again: assigns component and hides red error text.
            var added = c.gameObject.AddComponent<BoxCollider>();
            using (var e = NavigationSubmitEvent.GetPooled())
            {
                e.target = button;
                button.SendEvent(e);
            }
            for (var i = 0; i < 3; i++) yield return null;
            editor.serializedObject.Update();
            Assert.AreSame(added, c.box);
            Assert.AreEqual(DisplayStyle.None, error.resolvedStyle.display);
        }

        [UnityTest]
        public IEnumerator ReadOnlyAndShow_UpdateWhenCodeChangesValueWhileInspectorOpen()
        {
            var a = Component<ActionsFixture>();
            yield return Show(a);

            var readOnlyUi = window.rootVisualElement.Q<PropertyField>("readOnlyField").Q<IntegerField>();
            var showUi = window.rootVisualElement.Q<TextField>("hiddenSecret");
            var showBoolUi = window.rootVisualElement.Q<Toggle>("hiddenFlag");
            Assert.AreEqual(12, readOnlyUi.value);
            Assert.AreEqual("77", showUi.value);
            Assert.IsTrue(showBoolUi.value);

            // Code modifies fields directly while the inspector is open.
            a.readOnlyField = 45;
            a.SetSecret(123);
            a.SetHiddenFlag(false);
            a.AddToHiddenList("gamma");
            var until = EditorApplication.timeSinceStartup + 0.3;
            while (EditorApplication.timeSinceStartup < until) yield return null;

            Assert.AreEqual(45, readOnlyUi.value);
            Assert.AreEqual("123", showUi.value);
            Assert.IsFalse(showBoolUi.value);
            var listFoldout = window.rootVisualElement.Q<Foldout>("hiddenList");
            Assert.AreEqual(3, listFoldout.Q<IntegerField>("size").value);
            Assert.AreEqual("gamma", listFoldout.Q<TextField>("Element 2").value);
        }

        [UnityTest]
        public IEnumerator GetChild_AssignsMatchingChildComponentsOnly_AndShowsRedErrorWhenNone()
        {
            var c = Component<DrawerFixture>();
            // Parent itself has a SphereCollider, which must be excluded (children only).
            c.gameObject.AddComponent<SphereCollider>();
            yield return Show(c);

            var button = window.rootVisualElement.Q<Button>(className: "attributor-get-child");
            var error = window.rootVisualElement.Q<Label>(className: "attributor-get-child-error");
            Assert.IsNotNull(button);
            Assert.AreEqual(DisplayStyle.None, error.resolvedStyle.display);

            // Button lives in the header row: the list keeps the full width and the size field stays clear of it.
            var root = window.rootVisualElement.Q("childSpheres");
            Assert.AreEqual(root.layout.width, root.Q<ListView>().layout.width, 0.5f);
            var size = root.Q(className: ListView.arraySizeFieldUssClassName);
            Assert.LessOrEqual(size.worldBound.xMax, button.worldBound.xMin);
            Assert.Less(button.worldBound.yMin, size.worldBound.yMax);

            // No children with SphereCollider yet: warns, shows red error, leaves list empty.
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("SphereCollider"));
            using (var e = NavigationSubmitEvent.GetPooled())
            {
                e.target = button;
                button.SendEvent(e);
            }
            for (var i = 0; i < 3; i++) yield return null;
            Assert.AreEqual(0, c.childSpheres.Count);
            Assert.AreEqual(DisplayStyle.Flex, error.resolvedStyle.display);
            StringAssert.Contains("SphereCollider", error.text);

            // Add 3 children: two with SphereCollider, one without (only BoxCollider).
            var child1 = new GameObject("c1");
            child1.transform.SetParent(c.transform);
            var s1 = child1.AddComponent<SphereCollider>();

            var child2 = new GameObject("c2");
            child2.transform.SetParent(c.transform);
            child2.AddComponent<BoxCollider>();

            var child3 = new GameObject("c3");
            child3.transform.SetParent(c.transform);
            var s2 = child3.AddComponent<SphereCollider>();

            using (var e = NavigationSubmitEvent.GetPooled())
            {
                e.target = button;
                button.SendEvent(e);
            }
            for (var i = 0; i < 3; i++) yield return null;
            editor.serializedObject.Update();
            CollectionAssert.AreEqual(new[] { s1, s2 }, c.childSpheres);
            Assert.AreEqual(DisplayStyle.None, error.resolvedStyle.display);
        }
    
        [UnityTest]
        public IEnumerator Preview_ShowsSpriteThumbnail_OnlyWhileAssigned()
        {
            var c = Component<DrawerFixture>();
            yield return Show(c);
            var image = window.rootVisualElement.Q<Image>(className: "attributor-preview");
            Assert.AreEqual(DisplayStyle.None, image.resolvedStyle.display);

            editor.serializedObject.FindProperty("icon").objectReferenceValue = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            editor.serializedObject.ApplyModifiedProperties();
            var until = EditorApplication.timeSinceStartup + 3;
            while (EditorApplication.timeSinceStartup < until &&
                   window.rootVisualElement.Q<Image>(className: "attributor-preview").image == null) yield return null;
            image = window.rootVisualElement.Q<Image>(className: "attributor-preview");
            Assert.IsNotNull(image.image);
            Assert.AreEqual(DisplayStyle.Flex, image.resolvedStyle.display);
        }

        [UnityTest]
        public IEnumerator Gallery_OneCellPerElement_InColumns_AndFollowsListChanges()
        {
            var c = Component<DrawerFixture>();
            var sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            c.icons.AddRange(new[] { sprite, sprite });
            yield return Show(c);

            var grid = window.rootVisualElement.Q(className: "attributor-gallery");
            Assert.AreEqual(2, grid.childCount);
            Assert.AreEqual(Length.Percent(100f / 3), grid[0].style.width.value);

            var list = editor.serializedObject.FindProperty("icons");
            list.arraySize = 4;
            editor.serializedObject.ApplyModifiedProperties();
            var until = EditorApplication.timeSinceStartup + 0.5;
            while (EditorApplication.timeSinceStartup < until) yield return null;
            Assert.AreEqual(4, grid.childCount);
        }
    
        [UnityTest]
        public IEnumerator DropZone_ShownUnderListName_AppendsOnlyMatchingDrops()
        {
            var c = Component<DrawerFixture>();
            var existing = c.gameObject.AddComponent<BoxCollider>();
            c.droppedBoxes = new[] { existing };
            yield return Show(c);

            // Under the list's name row, above its elements.
            var zone = window.rootVisualElement.Q("droppedBoxes").Q("drop-zone");
            var header = zone.GetFirstAncestorOfType<Foldout>();
            Assert.IsTrue(header.ClassListContains(ListView.foldoutHeaderUssClassName));
            Assert.AreEqual(0, header.contentContainer.IndexOf(zone));

            var other = new GameObject("other");
            created.Add(other);
            var box = other.AddComponent<BoxCollider>();
            var plain = new GameObject("plain");
            created.Add(plain);

            // GameObjects resolve to their component; ones without it, and wrong types, are skipped.
            var added = MemberElements.AppendDropped(editor.serializedObject.FindProperty("droppedBoxes"), typeof(BoxCollider),
                new Object[] { other, plain, c.transform, box });
            Assert.AreEqual(2, added);
            CollectionAssert.AreEqual(new[] { existing, box, box }, c.droppedBoxes);
        }

        [Test]
        public void DropZone_SpriteFile_DropsAsItsSprite()
        {
            // Dragging a sprite .png from the Project window drags its Texture2D; the Sprite is a sub-asset.
            const string path = "Assets/AttributorDropTest.png";
            var tex = new Texture2D(4, 4);
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            try
            {
                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.SaveAndReimport();

                var c = Component<DrawerFixture>();
                var so = new SerializedObject(c);
                var added = MemberElements.AppendDropped(so.FindProperty("icons"), typeof(Sprite),
                    new Object[] { AssetDatabase.LoadMainAssetAtPath(path) });
                Assert.AreEqual(1, added);
                Assert.AreEqual(AssetDatabase.LoadAssetAtPath<Sprite>(path), c.icons[0]);
            }
            finally { AssetDatabase.DeleteAsset(path); }
        }
    }
}
