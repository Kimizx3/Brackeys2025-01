#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(DialogueStep))]
public class DialogueStepDrawer : PropertyDrawer
{
    const float Line = 18f;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property, label, true);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var stepTitleProp = property.FindPropertyRelative("stepTitle");
        var speakerProp = property.FindPropertyRelative("speaker");
        var contentProp = property.FindPropertyRelative("content");
        var kindProp = property.FindPropertyRelative("stepKind");

        string stepTitle = stepTitleProp != null ? stepTitleProp.stringValue : "";
        string speaker = speakerProp != null ? speakerProp.stringValue : "";
        string content = contentProp != null ? contentProp.stringValue : "";
        
        string preview = "";
        if (!string.IsNullOrEmpty(content))
        {
            preview = content.Replace("\n", " ");
            if (preview.Length > 18) preview = preview.Substring(0, 18) + "…";
        }
        
        string kind = kindProp != null ? kindProp.enumDisplayNames[kindProp.enumValueIndex] : "Step";
        
        string header = !string.IsNullOrEmpty(stepTitle)
            ? $"{stepTitle}  ({kind})"
            : $"{speaker}: {preview}  ({kind})";
        
        label.text = header;

        EditorGUI.PropertyField(position, property, label, true);
    }
}
#endif