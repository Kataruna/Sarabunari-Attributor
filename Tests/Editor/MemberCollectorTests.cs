using System.Linq;
using NUnit.Framework;
using Sarabunari.Attributor.Editor;
using UnityEngine;

namespace Sarabunari.Attributor.Tests
{
    public class MemberCollectorTests
    {
        class Plain : MonoBehaviour { public int x; }

        static string[] Names(System.Type t, System.Func<System.Type, string> sourceOf = null) =>
            MemberCollector.Collect(t, sourceOf).Select(m => m.Name).ToArray();

        [Test]
        public void ButtonBetweenFields_StaysInPlace()
        {
            CollectionAssert.AreEqual(new[] { "a", "B", "c" }, Names(typeof(OrderFixture)).Take(3));
        }

        [Test]
        public void ShowField_HasShowKind()
        {
            var s = MemberCollector.Collect(typeof(OrderFixture)).Single(m => m.Name == "s");
            Assert.AreEqual(MemberKind.Show, s.Kind);
            var b = MemberCollector.Collect(typeof(OrderFixture)).Single(m => m.Name == "B");
            Assert.AreEqual(MemberKind.Button, b.Kind);
        }

        [Test]
        public void BaseMembersFirst_EachFromOwnFile()
        {
            CollectionAssert.AreEqual(new[] { "b1", "BaseButton", "b2", "d1", "DerivedButton" }, Names(typeof(DerivedFixture)));
        }

        [Test]
        public void NameUsedInMethodAboveDeclaration_OrderStillCorrect()
        {
            CollectionAssert.AreEqual(new[] { "a", "B", "c", "s", "Early", "late", "AfterLate", "x" }, Names(typeof(OrderFixture)));
        }

        [Test]
        public void CharLiteralsVerbatimStringsAndExpressionBodies_DontMisplaceButtons()
        {
            CollectionAssert.AreEqual(new[] { "a", "open", "path", "B", "c" }, Names(typeof(TrickyFixture)));
        }

        [Test]
        public void SourceMissing_ButtonsAtEndOfClass_NoThrow()
        {
            CollectionAssert.AreEqual(new[] { "f", "Btn" }, Names(typeof(NotMatchingName)));
            var names = Names(typeof(OrderFixture), _ => null);
            CollectionAssert.AreEqual(new[] { "a", "c", "s", "late", "x" }, names.Take(5));
            CollectionAssert.AreEquivalent(new[] { "B", "Early", "AfterLate" }, names.Skip(5));
        }

        [Test]
        public void StackedLevels_KeepAttributeOrder()
        {
            var x = MemberCollector.Collect(typeof(OrderFixture)).Single(m => m.Name == "x");
            Assert.IsInstanceOf<FoldAttribute>(x.Levels[0]);
            Assert.IsInstanceOf<GroupAttribute>(x.Levels[1]);
        }

        [Test]
        public void HasAnyAttributorAttribute_FalseForPlainClass()
        {
            Assert.IsFalse(MemberCollector.HasAnyAttributorAttribute(typeof(Plain)));
            Assert.IsTrue(MemberCollector.HasAnyAttributorAttribute(typeof(OrderFixture)));
            Assert.IsTrue(MemberCollector.HasAnyAttributorAttribute(typeof(DerivedFixture)));
        }
    }
}
