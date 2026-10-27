using System;
using System.Collections.Generic;
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
        static readonly HashSet<string> Warned = new HashSet<string>();
        static readonly Dictionary<(Type, string), Func<object, bool>> Conditions = new Dictionary<(Type, string), Func<object, bool>>();
        static readonly Dictionary<Type, Dictionary<FieldInfo, object>> DefaultValues = new Dictionary<Type, Dictionary<FieldInfo, object>>();

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

        static string FormatValue(object val) =>
            (val is Object uo ? (uo != null ? uo.ToString() : "null") : val?.ToString()) ?? "null";

        public static VisualElement CreateShow(SerializedObject so, FieldInfo field)
        {
            SyncScriptDefault(so, field);
            var target = so.targetObject;
            var label = ObjectNames.NicifyVariableName(field.Name);

            if (typeof(System.Collections.IList).IsAssignableFrom(field.FieldType))
            {
                var elemType = field.FieldType.IsArray
                    ? field.FieldType.GetElementType()
                    : field.FieldType.IsGenericType ? field.FieldType.GetGenericArguments()[0] : typeof(object);

                var foldout = new Foldout { name = field.Name, text = label, value = true };
                foldout.AddToClassList("attributor-show-list");

                var toggle = foldout.Q<Toggle>(className: Foldout.toggleUssClassName);
                var countField = new IntegerField { name = "size", isReadOnly = true, style = { width = 48 } };
                countField.SetEnabled(false);
                if (toggle != null)
                {
                    var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
                    foldout.hierarchy.Insert(0, row);
                    toggle.style.flexGrow = 1;
                    row.Add(toggle);
                    row.Add(countField);
                }

                var lastList = CloneIfCollection(field.GetValue(target), field.FieldType);
                void RefreshList()
                {
                    var val = field.GetValue(target);
                    if (!ValueEquals(val, lastList))
                    {
                        lastList = CloneIfCollection(val, field.FieldType);
                        so.Update();
                    }
                    var list = val as System.Collections.IList;
                    var count = list?.Count ?? 0;
                    if (countField.value != count) countField.SetValueWithoutNotify(count);

                    while (foldout.childCount > count)
                        foldout.RemoveAt(foldout.childCount - 1);
                    while (foldout.childCount < count)
                    {
                        var idx = foldout.childCount;
                        if (elemType == typeof(bool))
                        {
                            var itemToggle = new Toggle($"Element {idx}") { name = $"Element {idx}" };
                            itemToggle.AddToClassList(BaseField<bool>.alignedFieldUssClassName);
                            itemToggle.SetEnabled(false);
                            foldout.Add(itemToggle);
                        }
                        else
                        {
                            var itemText = new TextField($"Element {idx}") { name = $"Element {idx}", isReadOnly = true };
                            itemText.AddToClassList(BaseField<string>.alignedFieldUssClassName);
                            itemText.SetEnabled(false);
                            foldout.Add(itemText);
                        }
                    }

                    for (var i = 0; i < count; i++)
                    {
                        var item = list[i];
                        if (elemType == typeof(bool) && foldout[i] is Toggle t)
                        {
                            var b = item is bool flag && flag;
                            if (t.value != b) t.SetValueWithoutNotify(b);
                        }
                        else if (foldout[i] is TextField tf)
                        {
                            var text = FormatValue(item);
                            if (tf.value != text) tf.SetValueWithoutNotify(text);
                        }
                    }
                }

                RefreshList();
                foldout.schedule.Execute(RefreshList).Every(100);
                return foldout;
            }

            if (field.FieldType == typeof(bool))
            {
                var toggle = new Toggle(label) { name = field.Name };
                toggle.AddToClassList(BaseField<bool>.alignedFieldUssClassName);
                toggle.SetEnabled(false);
                var lastBool = field.GetValue(target);
                void RefreshBool()
                {
                    var val = field.GetValue(target);
                    if (!Equals(val, lastBool))
                    {
                        lastBool = val;
                        so.Update();
                    }
                    var b = val is bool flag && flag;
                    if (toggle.value != b) toggle.SetValueWithoutNotify(b);
                }
                RefreshBool();
                toggle.schedule.Execute(RefreshBool).Every(100);
                return toggle;
            }

            var display = new TextField(label) { name = field.Name, isReadOnly = true };
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
                var text = FormatValue(val);
                if (display.value != text) display.SetValueWithoutNotify(text);
            }
            Refresh();
            display.schedule.Execute(Refresh).Every(100);
            return display;
        }

        static bool IsObjectOrObjectCollection(Type t)
        {
            if (typeof(Object).IsAssignableFrom(t)) return true;
            if (t.IsArray) return typeof(Object).IsAssignableFrom(t.GetElementType());
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>))
                return typeof(Object).IsAssignableFrom(t.GetGenericArguments()[0]);
            return false;
        }

        static Type ChildComponentType(FieldInfo field)
        {
            var t = field?.FieldType;
            if (t == null) return null;
            Type elem = null;
            if (t.IsArray && t.GetArrayRank() == 1) elem = t.GetElementType();
            else if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>)) elem = t.GetGenericArguments()[0];
            return elem != null && (typeof(Component).IsAssignableFrom(elem) || elem.IsInterface) ? elem : null;
        }

        public static VisualElement CreateGetChild(SerializedObject so, FieldInfo field, SerializedProperty property, GetChildAttribute attr)
        {
            var type = ChildComponentType(field);
            if (!property.isArray || type == null || !(so.targetObject is Component))
                return new HelpBox("GetChild works on List or array of Component fields", HelpBoxMessageType.Warning) { name = field.Name };

            var root = new VisualElement { name = field.Name };
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            var propField = new PropertyField(property) { style = { flexGrow = 1 } };
            var error = new Label($"No child has {type.Name} component")
            {
                style =
                {
                    color = new Color(0.95f, 0.3f, 0.3f),
                    display = DisplayStyle.None,
                }
            };
            error.AddToClassList("attributor-get-child-error");

            var button = new Button(() =>
            {
                var owner = (Component)so.targetObject;
                var found = new List<Component>();
                if (attr.Recursive)
                {
                    foreach (var c in owner.GetComponentsInChildren(type, attr.IncludeInactive))
                        if (c.gameObject != owner.gameObject) found.Add(c);
                }
                else
                {
                    var tr = owner.transform;
                    for (var i = 0; i < tr.childCount; i++)
                    {
                        var child = tr.GetChild(i);
                        if (!attr.IncludeInactive && !child.gameObject.activeInHierarchy) continue;
                        found.AddRange(child.GetComponents(type));
                    }
                }

                so.Update();
                if (found.Count == 0)
                {
                    property.ClearArray();
                    so.ApplyModifiedProperties();
                    error.style.display = DisplayStyle.Flex;
                    Debug.LogWarning($"Attributor: no child of {owner.name} has {type.Name} component", owner);
                    return;
                }

                error.style.display = DisplayStyle.None;
                property.arraySize = found.Count;
                for (var i = 0; i < found.Count; i++)
                    property.GetArrayElementAtIndex(i).objectReferenceValue = found[i];
                so.ApplyModifiedProperties();
            })
            {
                name = "get-child",
                text = "Get Child",
                style = { alignSelf = Align.FlexStart },
            };
            button.AddToClassList("attributor-get-child");

            propField.RegisterValueChangeCallback(_ =>
            {
                if (property.arraySize > 0)
                    error.style.display = DisplayStyle.None;
            });

            row.Add(propField);
            row.Add(button);
            root.Add(row);
            root.Add(error);
            return root;
        }

        static bool ValueEquals(object a, object b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a is System.Collections.IList la && b is System.Collections.IList lb)
            {
                if (la.Count != lb.Count) return false;
                for (var i = 0; i < la.Count; i++)
                    if (!Equals(la[i], lb[i])) return false;
                return true;
            }
            return Equals(a, b);
        }

        static object CloneIfCollection(object value, Type fieldType)
        {
            if (value is Array arr)
            {
                var copy = Array.CreateInstance(fieldType.GetElementType(), arr.Length);
                Array.Copy(arr, copy, arr.Length);
                return copy;
            }
            if (value is System.Collections.IList list && fieldType.IsGenericType)
            {
                var copy = (System.Collections.IList)Activator.CreateInstance(fieldType);
                foreach (var item in list) copy.Add(item);
                return copy;
            }
            return value;
        }

        static void SyncScriptDefault(SerializedObject so, FieldInfo field)
        {
            if (Application.isPlaying || IsObjectOrObjectCollection(field.FieldType)) return;
            var type = so.targetObject.GetType();
            if (!DefaultValues.TryGetValue(type, out var defaults))
            {
                defaults = new Dictionary<FieldInfo, object>();
                Object tempOwner = null;
                Object sample = null;
                try
                {
                    if (typeof(MonoBehaviour).IsAssignableFrom(type))
                    {
                        var go = new GameObject { hideFlags = HideFlags.HideAndDontSave };
                        go.SetActive(false);
                        tempOwner = go;
                        sample = go.AddComponent(type);
                    }
                    else if (typeof(ScriptableObject).IsAssignableFrom(type))
                    {
                        sample = tempOwner = ScriptableObject.CreateInstance(type);
                        sample.hideFlags = HideFlags.HideAndDontSave;
                    }
                    if (sample != null)
                        for (var t = type; t != null && t != typeof(object); t = t.BaseType)
                            foreach (var f in t.GetFields(All))
                                defaults[f] = CloneIfCollection(f.GetValue(sample), f.FieldType);
                }
                finally
                {
                    if (tempOwner != null) Object.DestroyImmediate(tempOwner);
                }
                DefaultValues[type] = defaults;
            }
            if (!defaults.TryGetValue(field, out var scriptValue) || scriptValue == null) return;
            var changed = false;
            foreach (var target in so.targetObjects)
            {
                if (ValueEquals(field.GetValue(target), scriptValue)) continue;
                field.SetValue(target, CloneIfCollection(scriptValue, field.FieldType));
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
                var last = CloneIfCollection(field.GetValue(target), field.FieldType);
                element.schedule.Execute(() =>
                {
                    var val = field.GetValue(target);
                    if (ValueEquals(val, last)) return;
                    last = CloneIfCollection(val, field.FieldType);
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
