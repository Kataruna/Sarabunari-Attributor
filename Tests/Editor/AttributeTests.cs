using System.Reflection;
using NUnit.Framework;

namespace Sarabunari.Attributor.Tests
{
    public class AttributeTests
    {
        class LineProbe
        {
            public const int ExpectedLine = 11;
            [Group("A")] public int x;
        }

        [Test]
        public void Group_CapturesPathColumnsAndLine()
        {
            var g = new GroupAttribute("Border/Inner", 2);
            Assert.AreEqual("Border/Inner", g.Path);
            Assert.AreEqual(2, g.Columns);
            Assert.Greater(g.Line, 0);
            Assert.IsFalse(g.Foldable);
        }

        [Test]
        public void BareGroup_HasNullPath()
        {
            var g = new GroupAttribute(3);
            Assert.IsNull(g.Path);
            Assert.AreEqual(3, g.Columns);
        }

        [Test]
        public void Fold_IsFoldable_DefaultColumnsUnspecified()
        {
            var f = new FoldAttribute("Border");
            Assert.IsTrue(f.Foldable);
            Assert.AreEqual(0, f.Columns);
        }

        [Test]
        public void LineNumber_CapturedFromAttributeUsage()
        {
            var a = typeof(LineProbe).GetField("x").GetCustomAttribute<GroupAttribute>();
            Assert.AreEqual(LineProbe.ExpectedLine, a.Line);
        }
    }
}
