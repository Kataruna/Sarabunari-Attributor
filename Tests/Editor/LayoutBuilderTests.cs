using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Sarabunari.Attributor.Editor;

namespace Sarabunari.Attributor.Tests
{
    public class LayoutBuilderTests
    {
        static MemberEntry M(string name, params LevelAttribute[] levels) =>
            new MemberEntry(name, MemberKind.Field, null, 0, levels);

        static GroupNode Build(List<string> warnings, params MemberEntry[] members) =>
            LayoutBuilder.Build(members, warnings);

        static GroupNode Build(params MemberEntry[] members) => Build(new List<string>(), members);

        static string Name(LayoutNode n) => n is MemberNode m ? m.Member.Name : ((GroupNode)n).Key;
        static string[] Names(GroupNode g) => g.Children.Select(Name).ToArray();
        static GroupNode G(GroupNode parent, int i) => (GroupNode)parent.Children[i];

        [Test]
        public void Ungrouped_KeepOrder()
        {
            var root = Build(M("a"), M("b"));
            Assert.IsNull(root.Title);
            Assert.AreEqual("", root.Key);
            Assert.AreEqual(1, root.Columns);
            CollectionAssert.AreEqual(new[] { "a", "b" }, Names(root));
        }

        [Test]
        public void SlashPath_Nests()
        {
            var root = Build(M("a", new GroupAttribute("Border/Inner", 2)));
            var border = G(root, 0);
            var inner = G(border, 0);
            Assert.AreEqual("Border", border.Title);
            Assert.AreEqual(1, border.Columns);
            Assert.AreEqual("Inner", inner.Title);
            Assert.AreEqual("/Border/Inner", inner.Key);
            Assert.AreEqual(2, inner.Columns);
            CollectionAssert.AreEqual(new[] { "a" }, Names(inner));
        }

        [Test]
        public void Stacking_EqualsPath_MixedFoldable()
        {
            var root = Build(M("a", new FoldAttribute("Border"), new GroupAttribute("Inner", 2)));
            var border = G(root, 0);
            var inner = G(border, 0);
            Assert.IsTrue(border.Foldable);
            Assert.IsFalse(inner.Foldable);
            Assert.AreEqual(2, inner.Columns);
            Assert.AreEqual("/Border/Inner", inner.Key);
        }

        [Test]
        public void Group_AppearsAtFirstMember_GathersLater()
        {
            var root = Build(M("a", new GroupAttribute("X")), M("b"), M("c", new GroupAttribute("X")));
            CollectionAssert.AreEqual(new[] { "/X", "b" }, Names(root));
            CollectionAssert.AreEqual(new[] { "a", "c" }, Names(G(root, 0)));
        }

        [Test]
        public void SiblingGroups_ShareParent()
        {
            var members = new List<MemberEntry>();
            foreach (var n in new[] { "o1", "o2", "o3", "o4" })
                members.Add(M(n, new FoldAttribute("Border"), new GroupAttribute("Outer", 2)));
            foreach (var n in new[] { "i1", "i2", "i3", "i4" })
                members.Add(M(n, new FoldAttribute("Border"), new GroupAttribute("Inner", 2)));
            var root = Build(members.ToArray());
            CollectionAssert.AreEqual(new[] { "/Border" }, Names(root));
            var border = G(root, 0);
            CollectionAssert.AreEqual(new[] { "/Border/Outer", "/Border/Inner" }, Names(border));
            CollectionAssert.AreEqual(new[] { "o1", "o2", "o3", "o4" }, Names(G(border, 0)));
            CollectionAssert.AreEqual(new[] { "i1", "i2", "i3", "i4" }, Names(G(border, 1)));
        }

        [Test]
        public void BareRow_ConsecutiveShareRow_BrokenByUngrouped()
        {
            var root = Build(M("a", new GroupAttribute(3)), M("b", new GroupAttribute(3)), M("c"), M("d", new GroupAttribute(3)));
            Assert.AreEqual(3, root.Children.Count);
            var row1 = G(root, 0);
            var row2 = G(root, 2);
            Assert.AreEqual("c", Name(root.Children[1]));
            Assert.IsNull(row1.Title);
            Assert.IsNull(row2.Title);
            Assert.AreEqual(3, row1.Columns);
            Assert.AreNotEqual(row1.Key, row2.Key);
            CollectionAssert.AreEqual(new[] { "a", "b" }, Names(row1));
            CollectionAssert.AreEqual(new[] { "d" }, Names(row2));
        }

        [Test]
        public void BareRow_InsideTitledGroup()
        {
            var root = Build(M("a", new FoldAttribute("Tools"), new GroupAttribute(2)),
                             M("b", new FoldAttribute("Tools"), new GroupAttribute(2)));
            var tools = G(root, 0);
            Assert.AreEqual("Tools", tools.Title);
            Assert.AreEqual(1, tools.Children.Count);
            var row = G(tools, 0);
            Assert.IsNull(row.Title);
            Assert.AreEqual(2, row.Columns);
            CollectionAssert.AreEqual(new[] { "a", "b" }, Names(row));
        }

        [Test]
        public void FoldAndGroup_SamePath_Foldable_Warns()
        {
            var warnings = new List<string>();
            var root = Build(warnings, M("a", new FoldAttribute("X")), M("b", new GroupAttribute("X")));
            Assert.IsTrue(G(root, 0).Foldable);
            Assert.AreEqual(1, warnings.Count);
        }

        [Test]
        public void ColumnConflict_LargestWins_Warns()
        {
            var warnings = new List<string>();
            var root = Build(warnings, M("a", new GroupAttribute("X", 2)), M("b", new GroupAttribute("X", 3)));
            Assert.AreEqual(3, G(root, 0).Columns);
            Assert.AreEqual(1, warnings.Count);
        }

        [Test]
        public void UnspecifiedColumns_DoNotConflict()
        {
            var warnings = new List<string>();
            var root = Build(warnings, M("a", new GroupAttribute("X", 2)), M("b", new GroupAttribute("X")));
            Assert.AreEqual(2, G(root, 0).Columns);
            Assert.AreEqual(0, warnings.Count);
        }
    }
}
