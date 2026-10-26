using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sarabunari.Attributor.Tests
{
    public class InspectorBuilderTests
    {
        GameObject go;
        UnityEditor.Editor editor;

        VisualElement Inspect<T>() where T : Component
        {
            go = new GameObject("test");
            editor = UnityEditor.Editor.CreateEditor(go.AddComponent<T>());
            return editor.CreateInspectorGUI();
        }

        [TearDown]
        public void TearDown()
        {
            if (editor != null) Object.DestroyImmediate(editor);
            if (go != null) Object.DestroyImmediate(go);
        }

        [Test]
        public void PlainClass_DefaultInspector()
        {
            var root = Inspect<PlainFixture>();
            Assert.IsNotNull(root);
            Assert.IsNotNull(root.Q<PropertyField>("PropertyField:x"));
            Assert.IsNotNull(root.Q<PropertyField>("PropertyField:inner"));
            Assert.IsNull(root.Q(className: "attributor-box"));
        }

        [Test]
        public void Border_IsFoldout_ContainsOuterThenInnerBoxes()
        {
            var root = Inspect<BorderFixture>();
            var border = root.Q<Foldout>("/Border");
            Assert.AreEqual("Border", border.text);
            var boxes = border.Query(className: "attributor-box").ToList().Select(e => e.name);
            CollectionAssert.AreEqual(new[] { "/Border/Outer", "/Border/Inner" }, boxes);
        }

        [Test]
        public void Outer_HasTwoColumns_FourFieldsInOrder()
        {
            var outer = Inspect<BorderFixture>().Q("/Border/Outer");
            var columns = outer.Q(className: "attributor-columns");
            var cells = columns.Query(className: "attributor-cell").ToList();
            Assert.AreEqual(4, cells.Count);
            CollectionAssert.AreEqual(new[] { "tl", "tr", "bl", "br" }, cells.Select(c => c.name));
            Assert.IsTrue(cells.All(c => c is PropertyField));
            Assert.AreEqual(Length.Percent(50), cells[0].style.width.value);
        }

        [Test]
        public void HiddenField_NotDrawn()
        {
            Assert.IsNull(Inspect<BorderFixture>().Q<PropertyField>("hidden"));
        }

        [Test]
        public void UngroupedAfterGroup_StaysAfter()
        {
            var root = Inspect<BorderFixture>();
            CollectionAssert.AreEqual(new[] { "m_Script", "/Border", "afterGroup" }, root.Children().Select(c => c.name));
        }
    }
}
