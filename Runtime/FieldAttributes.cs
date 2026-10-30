using System;
using UnityEngine;

namespace Sarabunari.Attributor
{
    /// <summary>Public or serialized field is shown in the inspector but locked from editing.</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class ReadOnlyAttribute : Attribute { }

    /// <summary>Alias for <see cref="ReadOnlyAttribute"/>.</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class LockedAttribute : ReadOnlyAttribute { }

    /// <summary>Field is editable only when the bool field, property or method is true.</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class EnableIfAttribute : Attribute
    {
        public string Condition { get; }
        public EnableIfAttribute(string condition) => Condition = condition;
    }

    /// <summary>Field is greyed out when the bool field, property or method is true.</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class DisableIfAttribute : Attribute
    {
        public string Condition { get; }
        public DisableIfAttribute(string condition) => Condition = condition;
    }

    /// <summary>Calls the named method after the field is changed in the inspector.</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class OnChangeAttribute : Attribute
    {
        public string Method { get; }
        public OnChangeAttribute(string method) => Method = method;
    }

    /// <summary>Separator line above the field.</summary>
    public class LineAttribute : PropertyAttribute { }

    /// <summary>Alias for <see cref="LineAttribute"/>.</summary>
    public class SeparatorAttribute : LineAttribute { }

    /// <summary>String field picks a scene from Build Settings.</summary>
    public class SceneNameAttribute : PropertyAttribute { }

    /// <summary>ScriptableObject reference shows its fields inline with an expand arrow.</summary>
    public class ExpandableAttribute : PropertyAttribute { }

    /// <summary>Alias for <see cref="ExpandableAttribute"/>.</summary>
    public class ExpandAttribute : ExpandableAttribute { }

    /// <summary>Vector2 field picks a range with a two-handle slider (x = min, y = max).</summary>
    public class MinMaxSliderAttribute : PropertyAttribute
    {
        public float Min { get; }
        public float Max { get; }
        public MinMaxSliderAttribute(float min, float max)
        {
            Min = min;
            Max = max;
        }
    }

    /// <summary>Object reference shows an error box while it is empty.</summary>
    public class RequiredAttribute : PropertyAttribute
    {
        public string Message { get; }
        public RequiredAttribute(string message = null) => Message = message;
    }

    /// <summary>Component field adds a button to assign the component from the same GameObject.</summary>
    public class SelfAssignAttribute : PropertyAttribute { }

    /// <summary>List or array of Components adds a button to populate from child GameObjects.</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class GetChildAttribute : Attribute
    {
        public bool Recursive { get; }
        public bool IncludeInactive { get; }

        public GetChildAttribute(bool recursive = true, bool includeInactive = true)
        {
            Recursive = recursive;
            IncludeInactive = includeInactive;
        }
    }

    /// <summary>Object reference shows a thumbnail of its texture, sprite, prefab or other asset under the field.</summary>
    public class PreviewAttribute : PropertyAttribute { }

    /// <summary>List or array of object references shows a thumbnail grid under the field.</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class GalleryAttribute : Attribute
    {
        public int Columns { get; }
        public GalleryAttribute(int columns = 4) => Columns = Math.Max(1, columns);
    }

    /// <summary>List or array of object references shows a drop area under its name; dropped objects are appended.</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class DropZoneAttribute : Attribute { }

    public enum PathPlane { XZ, XY, YZ }

    /// <summary>List or array of Vector3 shows a flat plot of its path under the field.</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class PathPreviewAttribute : Attribute
    {
        /// <summary>Axes the plot is projected onto.</summary>
        public PathPlane Plane { get; set; } = PathPlane.XZ;
    }

    /// <summary>List or array of Vector3 draws its path in the Scene view while the object is selected.</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class PathPreviewOnSceneAttribute : Attribute
    {
        /// <summary>Points are relative to the object's transform instead of world space.</summary>
        public bool Local { get; set; }
    }
}
