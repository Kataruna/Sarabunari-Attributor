using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;

namespace Sarabunari.Attributor.Editor
{
    /// <summary>Lists a type's inspector members in declaration order, base class first.</summary>
    public static class MemberCollector
    {
        const BindingFlags Declared = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        static readonly Dictionary<Type, List<MemberEntry>> CollectCache = new Dictionary<Type, List<MemberEntry>>();
        static readonly Dictionary<Type, bool> HasAnyCache = new Dictionary<Type, bool>();

        public static List<MemberEntry> Collect(Type type, Func<Type, string> sourceOf = null)
        {
            if (sourceOf != null) return CollectUncached(type, sourceOf);
            if (!CollectCache.TryGetValue(type, out var list))
                CollectCache[type] = list = CollectUncached(type, FindSource);
            return list;
        }

        public static bool HasAnyAttributorAttribute(Type type)
        {
            if (HasAnyCache.TryGetValue(type, out var result)) return result;
            result = Hierarchy(type).Any(t =>
                t.GetFields(Declared).Any(f => f.GetCustomAttributes(true).Any(IsOurs)) ||
                t.GetMethods(Declared).Any(m => m.IsDefined(typeof(ButtonAttribute), false)));
            return HasAnyCache[type] = result;
        }

        static bool IsOurs(object a) =>
            a is LevelAttribute || a is ButtonAttribute || a is ShowAttribute || a is ReadOnlyAttribute ||
            a is EnableIfAttribute || a is DisableIfAttribute || a is OnChangeAttribute || a is GetChildAttribute || a is GalleryAttribute || a is DropZoneAttribute;

        static IEnumerable<Type> Hierarchy(Type type)
        {
            var chain = new List<Type>();
            for (var t = type; t != null && t != typeof(object) && !IsUnityType(t); t = t.BaseType)
                chain.Add(t);
            chain.Reverse();
            return chain;
        }

        static bool IsUnityType(Type t) =>
            t.Namespace != null && (t.Namespace.StartsWith("UnityEngine") || t.Namespace.StartsWith("UnityEditor"));

        static List<MemberEntry> CollectUncached(Type type, Func<Type, string> sourceOf)
        {
            var result = new List<MemberEntry>();
            foreach (var t in Hierarchy(type))
                result.AddRange(CollectClass(t, sourceOf(t)));
            return result;
        }

        static List<MemberEntry> CollectClass(Type type, string source)
        {
            var finder = source == null ? null : new DeclarationFinder(source, type.Name);

            var fields = new List<MemberEntry>();
            var previousLine = -1;
            foreach (var f in type.GetFields(Declared).OrderBy(f => f.MetadataToken))
            {
                var show = f.GetCustomAttribute<ShowAttribute>();
                var levels = f.GetCustomAttributes<LevelAttribute>(false).ToList();
                var line = show?.Line ?? levels.FirstOrDefault()?.Line ?? 0;
                if (line == 0 && finder != null) line = finder.FindLine(SearchName(f.Name), previousLine);
                if (line <= 0) line = previousLine;
                previousLine = line;
                fields.Add(new MemberEntry(f.Name, show != null ? MemberKind.Show : MemberKind.Field, f, line, levels));
            }

            var buttons = type.GetMethods(Declared)
                .Select(m => (method: m, attr: m.GetCustomAttribute<ButtonAttribute>()))
                .Where(x => x.attr != null)
                .OrderBy(x => x.attr.Line).ThenBy(x => x.method.MetadataToken)
                .Select(x => new MemberEntry(x.method.Name, MemberKind.Button, x.method, x.attr.Line,
                    x.method.GetCustomAttributes<LevelAttribute>(false).ToList()))
                .ToList();

            // Without the script text, field positions are unknown: buttons go after the fields.
            var placeable = source == null ? new List<MemberEntry>() : buttons.Where(b => b.Line > 0).ToList();
            var atEnd = buttons.Except(placeable);

            var result = new List<MemberEntry>();
            var next = 0;
            foreach (var field in fields)
            {
                while (next < placeable.Count && placeable[next].Line < field.Line)
                    result.Add(placeable[next++]);
                result.Add(field);
            }
            result.AddRange(placeable.Skip(next));
            result.AddRange(atEnd);
            return result;
        }

        // Auto-property backing field "<X>k__BackingField" is declared as "X".
        static string SearchName(string fieldName)
        {
            var m = Regex.Match(fieldName, "^<(.+)>k__BackingField$");
            return m.Success ? m.Groups[1].Value : fieldName;
        }

        static string FindSource(Type type)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:MonoScript " + type.Name))
            {
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(guid));
                if (script != null && script.GetClass() == type) return script.text;
            }
            return null;
        }

        /// <summary>Finds field declaration lines: matches at the class body's depth only, so uses inside methods and attribute arguments are skipped.</summary>
        // ponytail: regex + bracket counting, a Roslyn parse if it ever misplaces members (e.g. strings nested in $"{...}")
        sealed class DeclarationFinder
        {
            readonly string source;
            readonly int[] depth; // { ( [ depth before each char, comments and literals ignored
            readonly List<int> lineStarts = new List<int> { 0 };
            readonly int classDepth = -1;

            public DeclarationFinder(string source, string className)
            {
                this.source = source;
                depth = new int[source.Length + 1];
                var d = 0;
                for (var i = 0; i < source.Length;)
                {
                    var end = LiteralEnd(source, i);
                    if (end > i)
                    {
                        while (i < end) depth[i++] = d;
                        continue;
                    }
                    depth[i] = d;
                    var c = source[i++];
                    if (c == '{' || c == '(' || c == '[') d++;
                    else if (c == '}' || c == ')' || c == ']') d--;
                }
                depth[source.Length] = d;

                for (var i = 0; i < source.Length; i++)
                    if (source[i] == '\n') lineStarts.Add(i + 1);

                var cls = Regex.Match(source, @"\b(class|struct)\s+" + Regex.Escape(className) + @"\b");
                if (cls.Success) classDepth = depth[cls.Index] + 1;
            }

            /// <summary>Index just past the comment, string or char literal starting at i, or i if none starts there.</summary>
            static int LiteralEnd(string s, int i)
            {
                var n = s.Length;
                if (s[i] == '/' && i + 1 < n && s[i + 1] == '/')
                {
                    var e = s.IndexOf('\n', i);
                    return e < 0 ? n : e;
                }
                if (s[i] == '/' && i + 1 < n && s[i + 1] == '*')
                {
                    var e = s.IndexOf("*/", i + 2, StringComparison.Ordinal);
                    return e < 0 ? n : e + 2;
                }
                var j = i;
                var verbatim = false;
                while (j < n && (s[j] == '@' || s[j] == '$')) verbatim |= s[j++] == '@';
                if (j >= n || !(s[j] == '"' || s[j] == '\'' && j == i)) return i;
                var quote = s[j];
                for (j++; j < n; j++)
                {
                    if (s[j] == '\\' && !verbatim) j++;
                    else if (s[j] == quote)
                    {
                        if (verbatim && j + 1 < n && s[j + 1] == quote) j++; // "" inside @"..."
                        else return j + 1;
                    }
                }
                return n;
            }

            // A declared name follows a type (int x, List<T> x, int[] x, int? x) or a comma (int a, b), not an operator.
            bool FollowsType(int index)
            {
                var k = index - 1;
                while (k >= 0 && char.IsWhiteSpace(source[k])) k--;
                if (k < 0) return false;
                var c = source[k];
                if (c == '>') return k == 0 || source[k - 1] != '='; // not "=> x;"
                return char.IsLetterOrDigit(c) || c == '_' || c == ']' || c == '?' || c == ',' || c == ')' || c == '*';
            }

            /// <summary>1-based line of the declaration of name after afterLine, or 0.</summary>
            public int FindLine(string name, int afterLine)
            {
                if (classDepth < 0) return 0;
                foreach (Match m in Regex.Matches(source, @"\b" + Regex.Escape(name) + @"\b\s*[;,=]"))
                {
                    if (depth[m.Index] != classDepth || !FollowsType(m.Index)) continue;
                    var k = lineStarts.BinarySearch(m.Index);
                    var line = k >= 0 ? k + 1 : ~k;
                    if (line >= afterLine) return line;
                }
                return 0;
            }
        }
    }
}
