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
        public IEnumerator Line_AndRange_BothApply()
        {
            yield return Show(Component<DrawerFixture>());
            Assert.IsNotNull(window.rootVisualElement.Q<Slider>());
            Assert.IsNotNull(window.rootVisualElement.Q(className: "attributor-line"));
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
    }
}
