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
            a is LevelAttribute || a is ButtonAttribute || a is ShowAttribute || a is LockedAttribute ||
            a is EnableIfAttribute || a is DisableIfAttribute || a is OnChangeAttribute;

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

        /// <summary>Finds field declaration lines: matches at the class body's brace depth only, so uses inside methods are skipped.</summary>
        // ponytail: regex + brace counting, a Roslyn parse if it ever misplaces members
        sealed class DeclarationFinder
        {
            readonly string source;
            readonly int[] depth; // brace depth before each char, comments and strings ignored
            readonly int classDepth = -1;

            public DeclarationFinder(string source, string className)
            {
                this.source = source;
                depth = new int[source.Length + 1];
                var d = 0;
                for (var i = 0; i < source.Length; i++)
                {
                    depth[i] = d;
                    var c = source[i];
                    if (c == '/' && i + 1 < source.Length && source[i + 1] == '/')
                    {
                        while (i < source.Length && source[i] != '\n') depth[i++] = d;
                        if (i < source.Length) depth[i] = d;
                    }
                    else if (c == '"')
                    {
                        depth[i++] = d;
                        while (i < source.Length && source[i] != '"') { if (source[i] == '\\') depth[i++] = d; if (i < source.Length) depth[i++] = d; }
                        if (i < source.Length) depth[i] = d;
                    }
                    else if (c == '{') d++;
                    else if (c == '}') d--;
                }
                depth[source.Length] = d;

                var cls = Regex.Match(source, @"\b(class|struct)\s+" + Regex.Escape(className) + @"\b");
                if (cls.Success) classDepth = depth[cls.Index] + 1;
            }

            /// <summary>1-based line of the declaration of name after afterLine, or 0.</summary>
            public int FindLine(string name, int afterLine)
            {
                if (classDepth < 0) return 0;
                foreach (Match m in Regex.Matches(source, @"\b" + Regex.Escape(name) + @"\b\s*[;,=]"))
                {
                    if (depth[m.Index] != classDepth) continue;
                    var before = source.Substring(0, m.Index).TrimEnd();
                    if (before.EndsWith("=>") || before.EndsWith(".")) continue;
                    var line = 1;
                    for (var i = 0; i < m.Index; i++) if (source[i] == '\n') line++;
                    if (line >= afterLine) return line;
                }
                return 0;
            }
        }
    }
}
