using System;
using UnityEngine;

namespace Sarabunari.Attributor
{
    /// <summary>Field is shown greyed out.</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class LockedAttribute : Attribute { }

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

    /// <summary>String field picks a scene from Build Settings.</summary>
    public class SceneNameAttribute : PropertyAttribute { }

    /// <summary>Object reference shows the referenced object's fields inline.</summary>
    public class ExpandAttribute : PropertyAttribute { }

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
}
