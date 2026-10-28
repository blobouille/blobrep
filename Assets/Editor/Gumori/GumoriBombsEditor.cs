using UnityEditor;
using UnityEngine;

// Bombes posées : poignée à glisser au centre de chacune dans la Scene view (hors Play), bouton pour en ajouter une.
[CustomEditor(typeof(GumoriBombs))]
public class GumoriBombsEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var bombs = (GumoriBombs)target;
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Scene view : glisser le point au centre d'une bombe posée pour la déplacer. Chiffre = tirs avant l'explosion, " +
                                "cercle rouge pâle = portée de l'onde (aperçu seulement, invisible en jeu).", MessageType.None);
        if (GUILayout.Button("Ajouter une bombe posée"))
        {
            Undo.RecordObject(bombs, "Ajouter une bombe");
            var sv = SceneView.lastActiveSceneView;
            Vector2 at = sv != null ? (Vector2)sv.pivot - (Vector2)bombs.transform.position : Vector2.zero;
            bombs.placed.Add(new GumoriBombs.Placed { position = at });
            EditorUtility.SetDirty(bombs);
        }
    }

    void OnSceneGUI()
    {
        if (Application.isPlaying) return;
        var bombs = (GumoriBombs)target;
        Vector2 origin = bombs.transform.position;
        for (int i = 0; i < bombs.placed.Count; i++)
        {
            Vector3 p = origin + bombs.placed[i].position;
            float size = HandleUtility.GetHandleSize(p) * 0.08f;
            Handles.color = Color.white;
            EditorGUI.BeginChangeCheck();
            var moved = Handles.FreeMoveHandle(p, size, Vector3.zero, Handles.DotHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(bombs, "Déplacer une bombe");
                bombs.placed[i].position = (Vector2)moved - origin;
                EditorUtility.SetDirty(bombs);
            }
        }
    }
}
