using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sarabunari.Attributor.Editor
{
    /// <summary>Turns a layout tree into UI Toolkit elements.</summary>
    public static class InspectorBuilder
    {
        const string StylePath = "Packages/com.sarabunari.attributor/Editor/Attributor.uss";

        public static VisualElement Build(SerializedObject so, GroupNode root)
        {
            var element = new VisualElement();
            var style = AssetDatabase.LoadAssetAtPath<StyleSheet>(StylePath);
            if (style != null) element.styleSheets.Add(style);

            var script = so.FindProperty("m_Script");
            if (script != null)
            {
                var field = new PropertyField(script) { name = "m_Script" };
                field.SetEnabled(false);
                element.Add(field);
            }

            // Fields declared in Unity base classes (e.g. Graphic.m_Color) aren't collected: draw them first, as-is.
            var collected = new HashSet<string>();
            CollectNames(root, collected);
            var property = so.GetIterator();
            for (var enter = true; property.NextVisible(enter); enter = false)
                if (property.name != "m_Script" && !collected.Contains(property.name))
                    element.Add(new PropertyField(property.Copy()) { name = property.name });

            AddChildren(element, so, root);
            return element;
        }

        static void CollectNames(GroupNode group, HashSet<string> names)
        {
            foreach (var child in group.Children)
                if (child is GroupNode g) CollectNames(g, names);
                else names.Add(((MemberNode)child).Member.Name);
        }

        public static VisualElement CreateMember(SerializedObject so, MemberEntry m)
        {
            if (m.Kind == MemberKind.Button)
                return MemberElements.CreateButton(so, (MethodInfo)m.Info, m.Info.GetCustomAttribute<ButtonAttribute>());
            var info = (FieldInfo)m.Info;
            if (m.Kind == MemberKind.Show) return MemberElements.CreateShow(so, info);
            if (info.IsDefined(typeof(HideInInspector), true)) return null;
            var property = so.FindProperty(m.Name);
            if (property == null) return null;
            var getChild = info.GetCustomAttribute<GetChildAttribute>();
            var field = getChild != null
                ? MemberElements.CreateGetChild(so, info, property, getChild)
                : new PropertyField(property) { name = m.Name };
            MemberElements.ApplyModifiers(field, so, info);
            return field;
        }

        static void AddChildren(VisualElement container, SerializedObject so, GroupNode group)
        {
            var target = container;
            if (group.Columns > 1)
            {
                target = new VisualElement();
                target.AddToClassList("attributor-columns");
                container.Add(target);
            }

            foreach (var child in group.Children)
            {
                var element = child is GroupNode g ? CreateGroup(so, g) : CreateMember(so, ((MemberNode)child).Member);
                if (element == null) continue;
                if (group.Columns > 1)
                {
                    element.AddToClassList("attributor-cell");
                    element.style.width = Length.Percent(100f / group.Columns);
                    // Columns are narrow: put labels above fields instead of Unity's aligned label column.
                    // Alignment may already have set an inline label width (near zero in right columns): clear it too.
                    element.RegisterCallback<GeometryChangedEvent>(_ =>
                        element.Query(className: BaseField<int>.alignedFieldUssClassName).ForEach(f =>
                        {
                            f.RemoveFromClassList(BaseField<int>.alignedFieldUssClassName);
                            var label = f.Q<Label>(className: BaseField<int>.labelUssClassName);
                            if (label == null) return;
                            label.style.width = StyleKeyword.Null;
                            label.style.minWidth = StyleKeyword.Null;
                        }));
                }
                target.Add(element);
            }
        }

        static VisualElement CreateGroup(SerializedObject so, GroupNode group)
        {
            VisualElement element;
            if (group.Title == null)
            {
                element = new VisualElement();
                element.AddToClassList("attributor-row");
            }
            else if (group.Foldable)
            {
                var key = "Attributor:" + so.targetObject.GetType().FullName + group.Key;
                var foldout = new Foldout { text = group.Title, value = SessionState.GetBool(key, true) };
                foldout.RegisterValueChangedCallback(e =>
                {
                    if (e.target == foldout) SessionState.SetBool(key, e.newValue);
                });
                foldout.AddToClassList("attributor-fold");
                element = foldout;
            }
            else
            {
                element = new VisualElement();
                element.AddToClassList("attributor-box");
                var title = new Label(group.Title);
                title.AddToClassList("attributor-title");
                element.Add(title);
            }

            element.name = group.Key;
            AddChildren(element, so, group);
            return element;
        }
    }
}
