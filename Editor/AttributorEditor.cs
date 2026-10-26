using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sarabunari.Attributor.Editor
{
    [CustomEditor(typeof(ScriptableObject), true), CanEditMultipleObjects]
    public class AttributorScriptableObjectEditor : AttributorEditor { }

    [CustomEditor(typeof(MonoBehaviour), true), CanEditMultipleObjects]
    public class AttributorEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var type = target.GetType();
            if (!MemberCollector.HasAnyAttributorAttribute(type))
            {
                // Never return null: that drops to IMGUI, where UI Toolkit-only drawers show "No GUI Implemented".
                var root = new VisualElement();
                InspectorElement.FillDefaultInspector(root, serializedObject, this);
                return root;
            }

            var warnings = new List<string>();
            var tree = LayoutBuilder.Build(MemberCollector.Collect(type), warnings);
            foreach (var warning in warnings) Debug.LogWarning(warning, target);
            return InspectorBuilder.Build(serializedObject, tree);
        }
    }
}
