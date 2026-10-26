using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Sarabunari.Attributor.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Sarabunari.Attributor.Tests
{
    public class MemberElementsTests
    {
        GameObject go1, go2;
        ActionsFixture a1, a2;

        [SetUp]
        public void SetUp()
        {
            a1 = (go1 = new GameObject("a1")).AddComponent<ActionsFixture>();
            a2 = (go2 = new GameObject("a2")).AddComponent<ActionsFixture>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(go1);
            Object.DestroyImmediate(go2);
        }

        static MethodInfo Method(string name) =>
            typeof(ActionsFixture).GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        static Button ButtonFor(Object target, string name)
        {
            var m = Method(name);
            return (Button)MemberElements.CreateButton(new SerializedObject(target), m, m.GetCustomAttribute<ButtonAttribute>());
        }

        [Test]
        public void Invoke_RunsOnAllTargets()
        {
            MemberElements.Invoke(Method("IncrementCounter"), new Object[] { a1, a2 });
            Assert.AreEqual(1, a1.counter);
            Assert.AreEqual(1, a2.counter);
        }

        [Test]
        public void Invoke_OptionalParameterMethod_Runs()
        {
            MemberElements.Invoke(Method("Save"), new Object[] { a1 });
            Assert.AreEqual(5, a1.last);
        }

        [Test]
        public void RequiredParameterMethod_ButtonDisabled()
        {
            var b = ButtonFor(a1, "Needs");
            Assert.IsFalse(b.enabledSelf);
            StringAssert.Contains("parameters", b.tooltip);
        }

        [Test]
        public void ButtonLabel_DefaultsToNicifiedName()
        {
            Assert.AreEqual("Increment Counter", ButtonFor(a1, "IncrementCounter").text);
            Assert.AreEqual("Go", ButtonFor(a1, "Go").text);
        }

        [Test]
        public void Condition_FieldPropertyMethod()
        {
            Assert.IsTrue(MemberElements.EvaluateCondition(a1, "flag"));
            Assert.IsFalse(MemberElements.EvaluateCondition(a1, "Prop"));
            Assert.IsTrue(MemberElements.EvaluateCondition(a1, "Method"));
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("missing"));
            Assert.IsFalse(MemberElements.EvaluateCondition(a1, "missing"));
        }

        [Test]
        public void ButtonsInBareRow_ThreeCells()
        {
            var editor = UnityEditor.Editor.CreateEditor(a1);
            try
            {
                var row = editor.CreateInspectorGUI().Q(className: "attributor-row");
                var buttons = row.Q(className: "attributor-columns").Query<Button>().ToList();
                CollectionAssert.AreEqual(new[] { "A", "B", "C" }, buttons.Select(b => b.name));
            }
            finally { Object.DestroyImmediate(editor); }
        }

        [Test]
        public void Locked_DisablesField()
        {
            var editor = UnityEditor.Editor.CreateEditor(a1);
            try
            {
                Assert.IsFalse(editor.CreateInspectorGUI().Q<PropertyField>("locked").enabledSelf);
            }
            finally { Object.DestroyImmediate(editor); }
        }
    }
}
