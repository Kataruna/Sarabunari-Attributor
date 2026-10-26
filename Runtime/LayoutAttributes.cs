using System;
using System.Runtime.CompilerServices;

namespace Sarabunari.Attributor
{
    /// <summary>One or more nesting levels ("A/B/C"). Path null = untitled bare row. Columns 0 = not specified.</summary>
    public abstract class LevelAttribute : Attribute
    {
        public string Path { get; }
        public int Columns { get; }
        public int Line { get; }
        public abstract bool Foldable { get; }

        protected LevelAttribute(string path, int columns, int line)
        {
            Path = path;
            Columns = columns;
            Line = line;
        }
    }

    /// <summary>Always-open titled box. [Group("Border/Inner", 2)] or a bare row [Group(3)].</summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Method, AllowMultiple = true)]
    public class GroupAttribute : LevelAttribute
    {
        public GroupAttribute(string path, int columns = 0, [CallerLineNumber] int line = 0) : base(path, columns, line) { }
        public GroupAttribute(int columns, [CallerLineNumber] int line = 0) : base(null, columns, line) { }
        public override bool Foldable => false;
    }

    /// <summary>Collapsible titled box. [Fold("Events")] or [Fold("Border/Inner", 2)].</summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Method, AllowMultiple = true)]
    public class FoldAttribute : LevelAttribute
    {
        public FoldAttribute(string path, int columns = 0, [CallerLineNumber] int line = 0) : base(path, columns, line) { }
        public override bool Foldable => true;
    }

    /// <summary>When a button can be clicked.</summary>
    public enum EButtonEnableMode { Always, Editor, Playmode }

    /// <summary>Draws a method as a button where it is declared.</summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class ButtonAttribute : Attribute
    {
        public string Label { get; }
        public EButtonEnableMode Mode { get; }
        public int Line { get; }

        public ButtonAttribute(string label = null, EButtonEnableMode mode = EButtonEnableMode.Always, [CallerLineNumber] int line = 0)
        {
            Label = label;
            Mode = mode;
            Line = line;
        }
    }

    /// <summary>Shows a non-serialized field, read-only.</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class ShowAttribute : Attribute
    {
        public int Line { get; }
        public ShowAttribute([CallerLineNumber] int line = 0) => Line = line;
    }
}
