using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
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
        static readonly HashSet<string> Warned = new HashSet<string>();
        static readonly Dictionary<(Type, string), Func<object, bool>> Conditions = new Dictionary<(Type, string), Func<object, bool>>();
        static readonly Dictionary<Type, object> DefaultInstances = new Dictionary<Type, object>();

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
            else if (attr.Mode != EButtonEnableMode.Always)
            {
                var playmode = attr.Mode == EButtonEnableMode.Playmode;
                void Refresh()
                {
                    var enabled = Application.isPlaying == playmode;
                    if (button.enabledSelf != enabled) button.SetEnabled(enabled);
                }
                Refresh();
                // Polled: the inspector isn't always rebuilt on entering Play mode (domain reload off).
                button.schedule.Execute(Refresh).Every(250);
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
                // A throwing method may have changed the object already: log and still mark it dirty, so the change isn't lost.
                try { method.Invoke(target, args); }
                catch (TargetInvocationException e) { Debug.LogException(e.InnerException, target); }
                EditorUtility.SetDirty(target);
                if (!Application.isPlaying && target is Component c && c.gameObject.scene.IsValid())
                    EditorSceneManager.MarkSceneDirty(c.gameObject.scene);
            }
        }

        public static VisualElement CreateShow(SerializedObject so, FieldInfo field)
        {
            SyncScriptDefault(so, field);
            var target = so.targetObject;
            var display = new TextField(ObjectNames.NicifyVariableName(field.Name)) { name = field.Name, isReadOnly = true };
            display.AddToClassList(BaseField<string>.alignedFieldUssClassName);
            display.SetEnabled(false);
            var last = field.GetValue(target);
            void Refresh()
            {
                var val = field.GetValue(target);
                if (!Equals(val, last))
                {
                    last = val;
                    so.Update();
                }
                var text = (val is Object uo ? (uo != null ? uo.ToString() : "null") : val?.ToString()) ?? "null";
                if (display.value != text) display.SetValueWithoutNotify(text);
            }
            Refresh();
            display.schedule.Execute(Refresh).Every(100);
            return display;
        }

        static void SyncScriptDefault(SerializedObject so, FieldInfo field)
        {
            if (Application.isPlaying || typeof(Object).IsAssignableFrom(field.FieldType)) return;
            var type = so.targetObject.GetType();
            if (!DefaultInstances.TryGetValue(type, out var sample))
            {
                sample = FormatterServices.GetUninitializedObject(type);
                var ctor = type.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                try { ctor?.Invoke(sample, null); }
                catch { /* Field initializers run before base/body; ignore Unity API calls inside user ctor. */ }
                DefaultInstances[type] = sample;
            }
            var scriptValue = field.GetValue(sample);
            if (scriptValue == null) return;
            var changed = false;
            foreach (var target in so.targetObjects)
            {
                if (Equals(field.GetValue(target), scriptValue)) continue;
                field.SetValue(target, scriptValue);
                EditorUtility.SetDirty(target);
                changed = true;
            }
            if (changed) so.Update();
        }

        public static bool EvaluateCondition(object target, string name)
        {
            // Polled several times a second per field: look the member up once per type.
            var key = (target.GetType(), name);
            if (!Conditions.TryGetValue(key, out var read)) Conditions[key] = read = FindCondition(key.Item1, name);
            if (read != null) return read(target);
            if (Warned.Add(target.GetType().FullName + "." + name))
                Debug.LogWarning($"Attributor: no bool field, property or method named '{name}' on {target.GetType().Name}");
            return false;
        }

        static Func<object, bool> FindCondition(Type type, string name)
        {
            for (var t = type; t != null; t = t.BaseType)
            {
                var field = t.GetField(name, All);
                if (field != null && field.FieldType == typeof(bool)) return o => (bool)field.GetValue(o);
                var property = t.GetProperty(name, All);
                if (property != null && property.PropertyType == typeof(bool)) return o => (bool)property.GetValue(o);
                var method = t.GetMethod(name, All, null, Type.EmptyTypes, null);
                if (method != null && method.ReturnType == typeof(bool)) return o => (bool)method.Invoke(o, null);
            }
            return null;
        }

        public static void ApplyModifiers(VisualElement element, SerializedObject so, FieldInfo field)
        {
            var readOnly = field.IsDefined(typeof(ReadOnlyAttribute), true);
            var enableIf = field.GetCustomAttribute<EnableIfAttribute>();
            var disableIf = field.GetCustomAttribute<DisableIfAttribute>();
            var onChange = field.GetCustomAttribute<OnChangeAttribute>();

            if (readOnly)
            {
                SyncScriptDefault(so, field);
                element.SetEnabled(false);
                var target = so.targetObject;
                var last = field.GetValue(target);
                element.schedule.Execute(() =>
                {
                    var val = field.GetValue(target);
                    if (Equals(val, last)) return;
                    last = val;
                    so.Update();
                }).Every(100);
            }
            else if (enableIf != null || disableIf != null)
            {
                var target = so.targetObject;
                var failed = false;
                void Refresh()
                {
                    if (failed) return;
                    try
                    {
                        var enabled =
                            (enableIf == null || EvaluateCondition(target, enableIf.Condition)) &&
                            (disableIf == null || !EvaluateCondition(target, disableIf.Condition));
                        if (element.enabledSelf != enabled) element.SetEnabled(enabled);
                    }
                    catch (Exception e)
                    {
                        failed = true; // log once, not every poll
                        Debug.LogException(e is TargetInvocationException t ? t.InnerException : e, target);
                    }
                }
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
