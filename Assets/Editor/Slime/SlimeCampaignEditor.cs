using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Campagne : un tableau par zone (un niveau par ligne : difficulté, coups, objectif calculé, coups nécessaires sans chaîne et
// marge réelle, comme l'inspecteur de SlimeGoal), réglages détaillés en dépliant la ligne, et « Jouer » pour lancer n'importe
// quel niveau tout de suite (même verrouillé). Outil de dev : rien de tout ça n'existe pour les joueurs.
[CustomEditor(typeof(SlimeCampaign))]
public class SlimeCampaignEditor : Editor
{
    const string LevelScene = "Assets/Scenes/Slime/reservoir.unity";

    readonly List<int> _sequence = new List<int>();
    readonly HashSet<string> _open = new HashSet<string>();
    GUIStyle _right, _small;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        var campaign = (SlimeCampaign)target;
        _right ??= new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleRight };
        _small ??= new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleLeft };

        EditorGUILayout.PropertyField(serializedObject.FindProperty("twoStarsMovesLeft"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("threeStarsMovesLeft"));

        // Règles du jeu lues dans le réservoir ouvert (niveau max des slimes, points par fusion, marges des difficultés).
        var goal = Object.FindAnyObjectByType<SlimeGoal>();
        var fusion = goal != null ? goal.GetComponent<BlobFusion>() : null;
        var score = goal != null ? goal.GetComponent<SlimeScore>() : null;
        bool rules = goal != null && fusion != null && score != null;
        if (!rules) EditorGUILayout.HelpBox("Ouvre la scène réservoir pour voir les objectifs calculés (mêmes réglages que le jeu).", MessageType.Info);

        var zones = serializedObject.FindProperty("zones");
        int index = 0;
        for (int z = 0; z < zones.arraySize; z++)
        {
            var zone = zones.GetArrayElementAtIndex(z);
            var levels = zone.FindPropertyRelative("levels");
            EditorGUILayout.Space(8f);
            EditorGUILayout.PropertyField(zone.FindPropertyRelative("name"), new GUIContent($"Zone {z + 1}"));
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("N°", EditorStyles.miniBoldLabel, GUILayout.Width(26f));
                GUILayout.Label("Difficulté", EditorStyles.miniBoldLabel, GUILayout.Width(74f));
                GUILayout.Label("Coups", EditorStyles.miniBoldLabel, GUILayout.Width(44f));
                GUILayout.Label("Objectif", EditorStyles.miniBoldLabel, GUILayout.Width(58f));
                GUILayout.Label("Sans chaîne", EditorStyles.miniBoldLabel);
            }
            for (int i = 0; i < levels.arraySize; i++, index++)
            {
                var lp = levels.GetArrayElementAtIndex(i);
                var level = z < campaign.zones.Count && i < campaign.zones[z].levels.Count ? campaign.zones[z].levels[i] : null;
                string key = lp.propertyPath;
                bool open = _open.Contains(key);
                using (new EditorGUILayout.HorizontalScope())
                {
                    var fold = GUILayoutUtility.GetRect(30f, EditorGUIUtility.singleLineHeight, GUILayout.Width(30f));
                    bool now = EditorGUI.Foldout(fold, open, (index + 1).ToString(), true);
                    if (now != open) { if (now) _open.Add(key); else _open.Remove(key); open = now; }
                    EditorGUILayout.PropertyField(lp.FindPropertyRelative("difficulty"), GUIContent.none, GUILayout.Width(74f));
                    EditorGUILayout.PropertyField(lp.FindPropertyRelative("maxDrops"), GUIContent.none, GUILayout.Width(40f));
                    string summary = "", targetText = "?";
                    if (rules && level != null) Summary(level, goal, fusion, score, out targetText, out summary);
                    else if (level != null && !level.autoTarget) targetText = level.targetScore.ToString("N0");
                    GUILayout.Label(targetText, _right, GUILayout.Width(58f));
                    GUILayout.Label(summary, _small);
                    int toPlay = index;
                    if (GUILayout.Button("Jouer", EditorStyles.miniButton, GUILayout.Width(44f))) EditorApplication.delayCall += () => Play(toPlay);
                }
                if (open)
                {
                    EditorGUI.indentLevel += 2;
                    foreach (var name in new[] { "seed", "autoTarget", "targetScore", "comboShare", "maxDropLevel", "smallBias", "note" })
                        EditorGUILayout.PropertyField(lp.FindPropertyRelative(name));
                    EditorGUI.indentLevel -= 2;
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("+ niveau", EditorStyles.miniButtonLeft))
                {
                    levels.InsertArrayElementAtIndex(levels.arraySize);
                    levels.GetArrayElementAtIndex(levels.arraySize - 1).FindPropertyRelative("seed").intValue = 100 + index + 1;
                }
                if (GUILayout.Button("− dernier", EditorStyles.miniButtonRight) && levels.arraySize > 1) levels.DeleteArrayElementAtIndex(levels.arraySize - 1);
            }
        }
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField($"Progression (ce poste) : {SlimeSession.Unlocked} niveau(x) débloqué(s)", EditorStyles.miniLabel);
        if (GUILayout.Button("Remettre la progression à zéro") &&
            EditorUtility.DisplayDialog("Progression", "Effacer la progression enregistrée (niveaux débloqués, étoiles, records) ?", "Effacer", "Annuler"))
            SlimeSession.ResetProgress();
    }

    void Summary(SlimeLevel level, SlimeGoal goal, BlobFusion fusion, SlimeScore score, out string targetText, out string summary)
    {
        int maxLevel = fusion.MaxLevel, ppm = score.pointsPerMerge;
        BlobDropper.PredictSequence(level.seed, Mathf.Min(level.maxDropLevel, maxLevel), level.smallBias, level.maxDrops, _sequence);
        int target = level.autoTarget ? SlimeGoal.AutoTarget(_sequence, level.maxDrops, maxLevel, ppm, goal.ExtraMovesFor(level.difficulty), level.comboShare) : level.targetScore;
        int needed = SlimeGoal.MovesNeeded(_sequence, level.maxDrops, target, maxLevel, ppm, level.comboShare);
        targetText = target.ToString("N0");
        if (needed == 0) { summary = "impossible"; return; }
        float margin = (float)level.maxDrops / needed - 1f;
        string label = margin >= goal.easyExtraMoves ? "facile" : margin >= goal.mediumExtraMoves ? "moyen" : margin >= goal.hardExtraMoves ? "difficile" : "très dur";
        summary = $"{needed} nécessaires, +{margin * 100f:0} % → {label}";
    }

    static void Play(int index)
    {
        if (EditorApplication.isPlaying)
        {
            SlimeSession.PlayLevel(index);
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (EditorSceneManager.GetActiveScene().path != LevelScene) EditorSceneManager.OpenScene(LevelScene);
        PlayerPrefs.SetInt(SlimeSession.DevLevelKey, index);
        PlayerPrefs.Save();
        EditorApplication.isPlaying = true;
    }
}
