// NB : les noms de la table ci-dessous ont été réécrits par le renommage lui-même (Blob… → Gumori…) ; les vrais chemins
// d'origine étaient Assets/Scripts/Slime/BlobDropper.cs, Assets/Scripts/Slime/SlimeSkin.cs, etc.
// Refonte du 2026-10-25 : suppression du code des anciens jeux et renommage Slime / Blob → Gumori, par l'AssetDatabase
// (les .meta suivent : scènes et valeurs gardées). Contenu des scripts réécrit à part, avant la recompilation.
var log = new System.Text.StringBuilder();
string[] delete =
{
    "Assets/Scripts/Legacy/Blob/BlobJump.cs", "Assets/Scripts/Skater", "Assets/Scripts/Game", "Assets/Scripts/Test/Ball2D.cs",
    "Assets/Scripts/Test/LevelShape.cs", "Assets/Scripts/Level/Destructibles", "Assets/Scripts/Level/Gadgets",
    "Assets/Scripts/Level/Hazards", "Assets/Scripts/Level/Neutral", "Assets/Scripts/Level/IPathRider.cs",
    "Assets/Scripts/Level/MovingPath.cs", "Assets/Scripts/Level/PathShapes.cs", "Assets/Scripts/Level/PinballBumper.cs",
    "Assets/Scripts/Level/PinballFlipper.cs", "Assets/Scripts/Cosmetics/ContactParticles.cs", "Assets/Scripts/Cosmetics/GlassDust.cs",
    "Assets/Scripts/Cosmetics/IContactFxSource.cs", "Assets/Scripts/Core/ITrainingBody.cs",
    "Assets/Editor/Game", "Assets/Editor/Level/BoostZoneEditor.cs", "Assets/Editor/Level/CircularSawEditor.cs",
    "Assets/Editor/Level/GadgetEditorUtil.cs", "Assets/Editor/Level/GadgetZoneEditor.cs", "Assets/Editor/Level/GlassPaneEditor.cs",
    "Assets/Editor/Level/GrapplePointEditor.cs", "Assets/Editor/Level/MovingPathEditor.cs", "Assets/Editor/Level/PinballBumperEditor.cs",
    "Assets/Editor/Level/SpikeHandles.cs", "Assets/Editor/Level/SpikeRowEditor.cs",
    "Assets/Scenes/Legacy", "Assets/Scenes/skate reboot.unity", "Assets/setup de base.prefab", "Assets/_Recovery",
};
string[,] move =
{
    { "Assets/Scripts/Legacy/Blob/B.L.O.B.cs", "Assets/Scripts/Slime/Gumori.cs" },
    { "Assets/Scripts/Test/mouse.cs", "Assets/Scripts/Core/LowLevelRaycastGrabber.cs" },
    { "Assets/Scripts/Core/WorldSettings.cs", "Assets/Scripts/Core/WorldSettings.cs" },
    { "Assets/Scripts/Slime/GumoriDropper.cs", "Assets/Scripts/Slime/GumoriDropper.cs" },
    { "Assets/Scripts/Slime/GumoriFusion.cs", "Assets/Scripts/Slime/GumoriFusion.cs" },
    { "Assets/Scripts/Slime/GumoriLauncher.cs", "Assets/Scripts/Slime/GumoriLauncher.cs" },
    { "Assets/Scripts/Slime/GumoriCampaign.cs", "Assets/Scripts/Slime/GumoriCampaign.cs" },
    { "Assets/Scripts/Slime/GumoriEscape.cs", "Assets/Scripts/Slime/GumoriEscape.cs" },
    { "Assets/Scripts/Slime/GumoriFoam.cs", "Assets/Scripts/Slime/GumoriFoam.cs" },
    { "Assets/Scripts/Slime/GumoriGoal.cs", "Assets/Scripts/Slime/GumoriGoal.cs" },
    { "Assets/Scripts/Slime/GumoriLevelSetup.cs", "Assets/Scripts/Slime/GumoriLevelSetup.cs" },
    { "Assets/Scripts/Slime/GumoriScore.cs", "Assets/Scripts/Slime/GumoriScore.cs" },
    { "Assets/Scripts/Slime/GumoriSession.cs", "Assets/Scripts/Slime/GumoriSession.cs" },
    { "Assets/Scripts/Slime/GumoriSoftMerge.cs", "Assets/Scripts/Slime/GumoriSoftMerge.cs" },
    { "Assets/Scripts/Slime/GumoriUi.cs", "Assets/Scripts/Slime/GumoriUi.cs" },
    { "Assets/Scripts/Slime/GumoriUntangle.cs", "Assets/Scripts/Slime/GumoriUntangle.cs" },
    { "Assets/Scripts/Slime/GumoriWallHold.cs", "Assets/Scripts/Slime/GumoriWallHold.cs" },
    { "Assets/Scripts/Cosmetics/GumoriBackdrop.cs", "Assets/Scripts/Cosmetics/GumoriBackdrop.cs" },
    { "Assets/Scripts/Cosmetics/GumoriDesign.cs", "Assets/Scripts/Cosmetics/GumoriDesign.cs" },
    { "Assets/Scripts/Cosmetics/GumoriFinale.cs", "Assets/Scripts/Cosmetics/GumoriFinale.cs" },
    { "Assets/Scripts/Cosmetics/GumoriFx.cs", "Assets/Scripts/Cosmetics/GumoriFx.cs" },
    { "Assets/Scripts/Cosmetics/GumoriSkin.cs", "Assets/Scripts/Cosmetics/GumoriSkin.cs" },
    { "Assets/Scripts/Cosmetics/GumoriSound.cs", "Assets/Scripts/Cosmetics/GumoriSound.cs" },
    { "Assets/Scripts/Cosmetics/GumoriSynth.cs", "Assets/Scripts/Cosmetics/GumoriSynth.cs" },
    { "Assets/Scripts/Map/GumoriMap.cs", "Assets/Scripts/Map/GumoriMap.cs" },
    { "Assets/Scripts/Map/GumoriMapAvatar.cs", "Assets/Scripts/Map/GumoriMapAvatar.cs" },
    { "Assets/Scripts/Map/GumoriMapMenu.cs", "Assets/Scripts/Map/GumoriMapMenu.cs" },
    { "Assets/Scripts/Map/GumoriMapMesh.cs", "Assets/Scripts/Map/GumoriMapMesh.cs" },
    { "Assets/Editor/Slime/GumoriCampaignEditor.cs", "Assets/Editor/Slime/GumoriCampaignEditor.cs" },
    { "Assets/Editor/Slime/GumoriGoalEditor.cs", "Assets/Editor/Slime/GumoriGoalEditor.cs" },
};
UnityEditor.AssetDatabase.StartAssetEditing();
try
{
    for (int i = 0; i < move.GetLength(0); i++)
    {
        string err = UnityEditor.AssetDatabase.MoveAsset(move[i, 0], move[i, 1]);
        log.Append(string.IsNullOrEmpty(err) ? "" : "MOVE " + move[i, 0] + " : " + err + "\n");
    }
    foreach (var p in delete)
        if (!UnityEditor.AssetDatabase.DeleteAsset(p)) log.Append("DEL échec " + p + "\n");
    foreach (var guid in UnityEditor.AssetDatabase.FindAssets("", new[] { "Assets/Temp" }))
    {
        string p = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
        if (p.EndsWith("SoundTap.cs") || !p.StartsWith("Assets/Temp/") || p.Substring(12).Contains("/")) continue;
        if (!UnityEditor.AssetDatabase.DeleteAsset(p)) log.Append("DEL échec " + p + "\n");
    }
    UnityEditor.AssetDatabase.DeleteAsset("Assets/Scripts/Legacy");
    UnityEditor.AssetDatabase.DeleteAsset("Assets/Scripts/Test");
}
finally
{
    UnityEditor.AssetDatabase.StopAssetEditing();
}
string err2 = UnityEditor.AssetDatabase.MoveAsset("Assets/Scripts/Slime", "Assets/Scripts/Gumori");
string err3 = UnityEditor.AssetDatabase.MoveAsset("Assets/Editor/Slime", "Assets/Editor/Gumori");
return log + "dossiers : " + err2 + " | " + err3;
