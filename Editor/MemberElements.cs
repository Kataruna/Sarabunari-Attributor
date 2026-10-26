using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Sarabunari.Attributor.Editor
{
    /// <summary>Elements for buttons and [Show] fields, and the per-field modifiers.</summary>
    public static class MemberElements
    {
        const BindingFlags All = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        public static VisualElement CreateButton(SerializedObject so, MethodInfo method, ButtonAttribute attr)
        {
            var targets = so.targetObjects;
            var button = new Button(() => Invoke(method, targets))
            {
                name = method.Name,
                text = attr.Label ?? ObjectNames.NicifyVariableName(method.Name),
            };
            if (method.GetParameters().Any(p => !p.HasDefaultValue))
            {
                button.SetEnabled(false);
                button.tooltip = "Attributor: Button methods need no parameters or only optional ones";
            }
            return button;
        }

        public static void Invoke(MethodInfo method, Object[] targets)
        {
            // A struct default like "= default" reports DefaultValue null; Invoke turns null into default(T).
            var args = method.GetParameters()
                .Select(p => p.DefaultValue is DBNull || p.DefaultValue == Missing.Value ? null : p.DefaultValue)
                .ToArray();
            foreach (var target in targets)
            {
                Undo.RecordObject(target, ObjectNames.NicifyVariableName(method.Name));
                method.Invoke(target, args);
                EditorUtility.SetDirty(target);
                if (!Application.isPlaying && target is Component c && c.gameObject.scene.IsValid())
                    EditorSceneManager.MarkSceneDirty(c.gameObject.scene);
            }
        }

        public static VisualElement CreateShow(SerializedObject so, FieldInfo field)
        {
            var target = so.targetObject;
            var display = new TextField(ObjectNames.NicifyVariableName(field.Name)) { name = field.Name, isReadOnly = true };
            display.AddToClassList(BaseField<string>.alignedFieldUssClassName);
            display.SetEnabled(false);
            void Refresh() => display.SetValueWithoutNotify(field.GetValue(target)?.ToString() ?? "null");
            Refresh();
            display.schedule.Execute(Refresh).Every(250);
            return display;
        }

        public static bool EvaluateCondition(object target, string name)
        {
            for (var t = target.GetType(); t != null; t = t.BaseType)
            {
                var field = t.GetField(name, All);
                if (field != null && field.FieldType == typeof(bool)) return (bool)field.GetValue(target);
                var property = t.GetProperty(name, All);
                if (property != null && property.PropertyType == typeof(bool)) return (bool)property.GetValue(target);
                var method = t.GetMethod(name, All, null, Type.EmptyTypes, null);
                if (method != null && method.ReturnType == typeof(bool)) return (bool)method.Invoke(target, null);
            }
            Debug.LogWarning($"Attributor: no bool field, property or method named '{name}' on {target.GetType().Name}");
            return false;
        }

        public static void ApplyModifiers(VisualElement element, SerializedObject so, FieldInfo field)
        {
            var locked = field.IsDefined(typeof(LockedAttribute), true);
            var enableIf = field.GetCustomAttribute<EnableIfAttribute>();
            var disableIf = field.GetCustomAttribute<DisableIfAttribute>();
            var onChange = field.GetCustomAttribute<OnChangeAttribute>();

            if (locked) element.SetEnabled(false);
            else if (enableIf != null || disableIf != null)
            {
                var target = so.targetObject;
                void Refresh() => element.SetEnabled(
                    (enableIf == null || EvaluateCondition(target, enableIf.Condition)) &&
                    (disableIf == null || !EvaluateCondition(target, disableIf.Condition)));
                Refresh();
                // ponytail: polling, switch to change events if inspectors get heavy
                element.schedule.Execute(Refresh).Every(100);
            }

            if (onChange != null)
            {
                var method = FindMethod(field.DeclaringType, onChange.Method);
                if (method == null) Debug.LogWarning($"Attributor: OnChange method '{onChange.Method}' not found on {field.DeclaringType.Name}");
                // Fires only on real changes, not when the field is first bound.
                else element.TrackPropertyValue(so.FindProperty(field.Name), _ =>
                {
                    foreach (var target in so.targetObjects) method.Invoke(target, null);
                });
            }
        }

        static MethodInfo FindMethod(Type type, string name)
        {
            for (var t = type; t != null; t = t.BaseType)
            {
                var m = t.GetMethod(name, All, null, Type.EmptyTypes, null);
                if (m != null) return m;
            }
            return null;
        }
    }
}
