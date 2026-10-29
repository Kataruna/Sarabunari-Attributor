using System.Collections.Generic;
using System.Reflection;

namespace Sarabunari.Attributor.Editor
{
    public enum MemberKind { Field, Button, Show }

    public sealed class MemberEntry
    {
        public string Name { get; }
        public MemberKind Kind { get; }
        public MemberInfo Info { get; }
        public int Line { get; }
        public IReadOnlyList<LevelAttribute> Levels { get; }
        /// <summary>Which [Button] this entry draws: a method can have several.</summary>
        public ButtonAttribute Button { get; }

        public MemberEntry(string name, MemberKind kind, MemberInfo info, int line, IReadOnlyList<LevelAttribute> levels, ButtonAttribute button = null)
        {
            Button = button;
            Name = name;
            Kind = kind;
            Info = info;
            Line = line;
            Levels = levels;
        }
    }

    public abstract class LayoutNode { }

    public sealed class MemberNode : LayoutNode
    {
        public MemberEntry Member { get; }
        public MemberNode(MemberEntry member) => Member = member;
    }

    public sealed class GroupNode : LayoutNode
    {
        /// <summary>Null for the root and for bare rows.</summary>
        public string Title { get; internal set; }
        public string Key { get; internal set; }
        public bool Foldable { get; internal set; }
        public int Columns { get; internal set; }
        public List<LayoutNode> Children { get; } = new List<LayoutNode>();
    }

    public static class LayoutBuilder
    {
        public static GroupNode Build(IReadOnlyList<MemberEntry> orderedMembers, List<string> warnings)
        {
            var root = new GroupNode { Key = "" };
            var groups = new Dictionary<string, GroupNode> { [""] = root };
            var warned = new HashSet<string>();
            var rowCount = 0;
            // parent key -> bare row key, for rows used by the previous member
            var previousRows = new Dictionary<string, string>();

            foreach (var member in orderedMembers)
            {
                var currentRows = new Dictionary<string, string>();
                var parent = root;

                foreach (var level in member.Levels)
                {
                    var titles = level.Path == null ? new string[] { null } : level.Path.Split('/');
                    for (var i = 0; i < titles.Length; i++)
                    {
                        var title = titles[i];
                        var columns = i == titles.Length - 1 ? level.Columns : 0;
                        string key;
                        if (title == null)
                        {
                            if (!previousRows.TryGetValue(parent.Key, out key))
                                key = parent.Key + "/#row" + rowCount++;
                            currentRows[parent.Key] = key;
                        }
                        else key = parent.Key + "/" + title;

                        if (!groups.TryGetValue(key, out var group))
                        {
                            group = new GroupNode { Title = title, Key = key, Foldable = level.Foldable, Columns = columns };
                            groups[key] = group;
                            parent.Children.Add(group);
                        }
                        else
                        {
                            if (group.Foldable != level.Foldable && warned.Add(key + "#fold"))
                                warnings.Add($"Attributor: group '{key}' declared as both Fold and Group");
                            group.Foldable |= level.Foldable;
                            if (columns != 0 && group.Columns != 0 && columns != group.Columns && warned.Add(key + "#cols"))
                                warnings.Add($"Attributor: group '{key}' has conflicting column counts");
                            if (columns > group.Columns) group.Columns = columns;
                        }
                        parent = group;
                    }
                }

                parent.Children.Add(new MemberNode(member));
                previousRows = currentRows;
            }

            foreach (var g in groups.Values)
                if (g.Columns == 0) g.Columns = 1;
            return root;
        }
    }
}
