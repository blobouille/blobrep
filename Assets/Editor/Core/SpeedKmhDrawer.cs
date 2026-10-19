using System.Reflection;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(SpeedKmhAttribute))]
public class SpeedKmhDrawer : PropertyDrawer
{
    const float UnitWidth = 36f;
    const float K = SpeedKmhAttribute.KmhPerMs;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        float min = fieldInfo.GetCustomAttribute<MinAttribute>() is MinAttribute m ? m.min : float.NegativeInfinity;
        var field = new Rect(position.x, position.y, position.width - UnitWidth, position.height);
        var unit = new Rect(field.xMax + 4f, position.y, UnitWidth - 4f, position.height);

        EditorGUI.BeginProperty(position, label, property);
        switch (property.propertyType)
        {
            case SerializedPropertyType.Float:
            {
                float ms = property.floatValue;
                label.tooltip += $"\n\nSaisie en km/h, stocké en m/s ({ms:0.###} m/s).";
                EditorGUI.BeginChangeCheck();
                float kmh = EditorGUI.FloatField(field, label, ms * K);
                if (EditorGUI.EndChangeCheck()) property.floatValue = Mathf.Max(min, kmh / K);
                break;
            }
            case SerializedPropertyType.Vector2:
            {
                Vector2 ms = property.vector2Value;
                label.tooltip += $"\n\nSaisie en km/h, stocké en m/s ({ms.x:0.###}, {ms.y:0.###} m/s).";
                EditorGUI.BeginChangeCheck();
                Vector2 kmh = EditorGUI.Vector2Field(field, label, ms * K);
                if (EditorGUI.EndChangeCheck()) property.vector2Value = kmh / K;
                break;
            }
            default:
                EditorGUI.PropertyField(position, property, label);
                EditorGUI.EndProperty();
                return;
        }
        EditorGUI.LabelField(unit, "km/h", EditorStyles.miniLabel);
        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
        property.propertyType == SerializedPropertyType.Vector2
            ? EditorGUI.GetPropertyHeight(SerializedPropertyType.Vector2, label)
            : EditorGUIUtility.singleLineHeight;
}
