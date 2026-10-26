using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sarabunari.Attributor.Editor
{
    [CustomPropertyDrawer(typeof(LineAttribute))]
    public class LineDrawer : DecoratorDrawer
    {
        public override VisualElement CreatePropertyGUI()
        {
            // Inline style: plain inspectors don't load Attributor.uss.
            var line = new VisualElement();
            line.AddToClassList("attributor-line");
            line.style.height = 1;
            line.style.marginTop = line.style.marginBottom = 4;
            line.style.backgroundColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            return line;
        }
    }

    [CustomPropertyDrawer(typeof(SceneNameAttribute))]
    public class SceneNameDrawer : PropertyDrawer
    {
        public static List<string> Options() =>
            EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => Path.GetFileNameWithoutExtension(s.path)).ToList();

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            if (property.propertyType != SerializedPropertyType.String)
                return new HelpBox("SceneName works on string fields", HelpBoxMessageType.Warning);

            var options = Options();
            var choices = new List<string> { "" };
            choices.AddRange(options);
            if (!choices.Contains(property.stringValue)) choices.Add(property.stringValue);

            string Format(string s) => s == "" ? "(none)" : options.Contains(s) ? s : s + " (missing)";
            var popup = new PopupField<string>(property.displayName, choices, property.stringValue, Format, Format);
            popup.AddToClassList(BaseField<string>.alignedFieldUssClassName);
            popup.BindProperty(property);
            return popup;
        }
    }

    [CustomPropertyDrawer(typeof(MinMaxSliderAttribute))]
    public class MinMaxSliderDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            if (property.propertyType != SerializedPropertyType.Vector2)
                return new HelpBox("MinMaxSlider works on Vector2 fields", HelpBoxMessageType.Warning);

            var attr = (MinMaxSliderAttribute)attribute;
            var slider = new MinMaxSlider(property.displayName, property.vector2Value.x, property.vector2Value.y, attr.Min, attr.Max);
            slider.AddToClassList(BaseField<Vector2>.alignedFieldUssClassName);
            slider.style.flexGrow = 1;
            slider.BindProperty(property);

            // Typed values are clamped to the limits and kept in order, like dragging.
            FloatField Number(string axis)
            {
                var number = new FloatField { name = axis };
                number.style.width = 50;
                number.BindProperty(property.FindPropertyRelative(axis));
                number.RegisterValueChangedCallback(e =>
                {
                    var v = property.vector2Value;
                    var clamped = axis == "x" ? Mathf.Clamp(e.newValue, attr.Min, v.y) : Mathf.Clamp(e.newValue, v.x, attr.Max);
                    if (clamped != e.newValue) number.value = clamped;
                });
                return number;
            }

            var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            row.Add(slider);
            row.Add(Number("x"));
            row.Add(Number("y"));
            return row;
        }
    }

    [CustomPropertyDrawer(typeof(RequiredAttribute))]
    public class RequiredDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            if (property.propertyType != SerializedPropertyType.ObjectReference)
                return new HelpBox("Required works on object reference fields", HelpBoxMessageType.Warning);

            var root = new VisualElement();
            var field = new PropertyField(property);
            root.Add(field);
            var message = ((RequiredAttribute)attribute).Message ?? property.displayName + " is required";
            var warning = new HelpBox(message, HelpBoxMessageType.Error);
            warning.AddToClassList("attributor-required");
            root.Add(warning);

            void Refresh(SerializedProperty p) =>
                warning.style.display = p.objectReferenceValue == null ? DisplayStyle.Flex : DisplayStyle.None;
            Refresh(property);
            field.RegisterValueChangeCallback(e => Refresh(e.changedProperty));
            return root;
        }
    }

    [CustomPropertyDrawer(typeof(ExpandAttribute))]
    public class ExpandDrawer : PropertyDrawer
    {
        const int MaxDepth = 3;

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement();
            var field = new PropertyField(property);
            root.Add(field);
            if (property.propertyType != SerializedPropertyType.ObjectReference) return root;

            var owner = property.serializedObject.targetObject;
            var foldout = new Foldout { value = false };
            foldout.AddToClassList("attributor-expand");
            root.Add(foldout);

            void Refresh(Object obj)
            {
                foldout.Clear();
                foldout.userData = obj;
                foldout.text = obj == null ? "" : obj.name == "" ? obj.GetType().Name : obj.name;
                foldout.style.display = obj == null ? DisplayStyle.None : DisplayStyle.Flex;
                foldout.value = false;
            }

            // Content is built only when opened, and never for an object already open above.
            foldout.RegisterValueChangedCallback(e =>
            {
                if (e.target != foldout || !e.newValue || foldout.childCount > 0 || !(foldout.userData is Object obj)) return;
                var ancestors = new List<Object> { owner };
                for (var p = foldout.parent; p != null; p = p.parent)
                    if (p.ClassListContains("attributor-expand") && p.userData is Object o) ancestors.Add(o);
                if (ancestors.Contains(obj) || ancestors.Count > MaxDepth)
                {
                    var skip = new Label("Already shown above");
                    skip.AddToClassList("attributor-expand-skip");
                    foldout.Add(skip);
                }
                else foldout.Add(new InspectorElement(obj));
            });

            Refresh(property.objectReferenceValue);
            field.RegisterValueChangeCallback(e => Refresh(e.changedProperty.objectReferenceValue));
            return root;
        }
    }
}
