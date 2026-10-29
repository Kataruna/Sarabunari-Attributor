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
        public void Invoke_ThrowingMethod_LogsAndRunsOtherTargets()
        {
            LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("boom"));
            LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("boom"));
            MemberElements.Invoke(Method("Boom"), new Object[] { a1, a2 });
            Assert.AreEqual(9, a1.counter);
            Assert.AreEqual(9, a2.counter);
        }

        [Test]
        public void RequiredParameterMethod_ButtonDisabled()
        {
            var b = ButtonFor(a1, "Needs");
            Assert.IsFalse(b.enabledSelf);
            StringAssert.Contains("no default", b.tooltip);
        }

        static void Click(Object target, MemberEntry entry)
        {
            var m = (MethodInfo)entry.Info;
            var b = (Button)MemberElements.CreateButton(new SerializedObject(target), m, entry.Button);
            Assert.IsTrue(b.enabledSelf, b.tooltip);
            MemberElements.Invoke(m, new[] { target }, MemberElements.ButtonArgs(m, entry.Button.Args, out _));
        }

        [Test]
        public void ButtonArguments_OneButtonPerAttribute_InOrder_PassingTheirValues()
        {
            var entries = MemberCollector.Collect(typeof(ActionsFixture)).Where(e => e.Name == "AddHours").ToList();
            CollectionAssert.AreEqual(new[] { "Add 1 Hour", "Add 2 Hours" }, entries.Select(e => e.Button.Label));
            Click(a1, entries[0]);
            Assert.AreEqual(1, a1.counter);
            Click(a1, entries[1]);
            Assert.AreEqual(3, a1.counter);
        }

        [Test]
        public void ButtonArguments_LiteralZero_IsAnArgumentNotAMode()
        {
            var all = MemberCollector.Collect(typeof(ActionsFixture));
            a1.last = 7;
            Click(a1, all.Single(e => e.Name == "SetLast"));
            Assert.AreEqual(0, a1.last);
            Click(a1, all.Single(e => e.Name == "SetPair"));
            Assert.AreEqual(5, a1.last);
        }

        [Test]
        public void ButtonArguments_ConvertedToParameterType()
        {
            Click(a1, MemberCollector.Collect(typeof(ActionsFixture)).Single(e => e.Name == "SetSpeed"));
            Assert.AreEqual(1f, a1.speed);
        }

        [Test]
        public void ButtonArguments_WrongTypeOrCount_ButtonDisabledWithReason()
        {
            var bad = ButtonFor(a1, "TakesInt");
            Assert.IsFalse(bad.enabledSelf);
            StringAssert.Contains("text", bad.tooltip);
            var many = ButtonFor(a1, "TakesOne");
            Assert.IsFalse(many.enabledSelf);
            StringAssert.Contains("passes 2", many.tooltip);
        }

        [Test]
        public void PlaymodeButton_DisabledInEditMode()
        {
            var b = ButtonFor(a1, "PlayOnly");
            Assert.AreEqual("Play", b.text);
            Assert.IsFalse(b.enabledSelf);
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
                var root = editor.CreateInspectorGUI();
                Assert.IsFalse(root.Q<PropertyField>("locked").enabledSelf);
                Assert.IsFalse(root.Q<PropertyField>("readOnlyField").enabledSelf);
            }
            finally { Object.DestroyImmediate(editor); }
        }

        [Test]
        public void Show_DisplaysPrivateFieldReadOnly()
        {
            var editor = UnityEditor.Editor.CreateEditor(a1);
            try
            {
                var root = editor.CreateInspectorGUI();
                var field = root.Q<TextField>("hiddenSecret");
                Assert.IsNotNull(field);
                Assert.AreEqual("77", field.value);
                Assert.IsTrue(field.isReadOnly);
                Assert.IsFalse(field.enabledSelf);

                var toggle = root.Q<Toggle>("hiddenFlag");
                Assert.IsNotNull(toggle);
                Assert.IsTrue(toggle.value);
                Assert.IsFalse(toggle.enabledSelf);

                var arrayFoldout = root.Q<Foldout>("hiddenArray");
                Assert.IsNotNull(arrayFoldout);
                Assert.AreEqual(2, arrayFoldout.Q<IntegerField>("size").value);
                Assert.AreEqual("10", arrayFoldout.Q<TextField>("Element 0").value);
                Assert.AreEqual("20", arrayFoldout.Q<TextField>("Element 1").value);

                var listFoldout = root.Q<Foldout>("hiddenList");
                Assert.IsNotNull(listFoldout);
                Assert.AreEqual(2, listFoldout.Q<IntegerField>("size").value);
                Assert.AreEqual("alpha", listFoldout.Q<TextField>("Element 0").value);
                Assert.AreEqual("beta", listFoldout.Q<TextField>("Element 1").value);
            }
            finally { Object.DestroyImmediate(editor); }
        }

        [Test]
        public void ReadOnlyAndShow_SyncStaleValueToScriptDefaultOnInspectorBuild()
        {
            // Simulate stale scene serialization that differs from the C# script initializers (12 and 77).
            a1.readOnlyField = 999;
            a1.SetSecret(888);
            var editor = UnityEditor.Editor.CreateEditor(a1);
            try
            {
                var root = editor.CreateInspectorGUI();
                Assert.AreEqual(12, a1.readOnlyField);
                Assert.AreEqual(12, editor.serializedObject.FindProperty("readOnlyField").intValue);
                Assert.AreEqual("77", root.Q<TextField>("hiddenSecret").value);
            }
            finally { Object.DestroyImmediate(editor); }
        }
    }
}
