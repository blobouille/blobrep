// 2026-10-27 : range Scripts/Gumori en sous-dossiers (mêmes groupes que les sous-objets de « Jeu »). Par AssetDatabase : les
// .meta suivent, les scènes gardent leurs références.
var moves = new (string file, string folder)[]
{
    ("Gumori", "Corps"), ("GumoriFoam", "Corps"), ("GumoriUntangle", "Corps"), ("GumoriWallHold", "Corps"),
    ("GumoriFusion", "Fusion"), ("GumoriEscape", "Fusion"),
    ("GumoriScore", "Règles"), ("GumoriGoal", "Règles"), ("SpaceEndless", "Règles"), ("SpaceLevel", "Règles"),
    ("GumoriLauncher", "Réserve"),
    ("GumoriJars", "Mécaniques"), ("GumoriMill", "Mécaniques"),
    ("GumoriCampaign", "Parties"), ("GumoriSession", "Parties"), ("GumoriLevelSetup", "Parties"), ("GumoriUi", "Parties"),
};
var log = new System.Text.StringBuilder();
foreach (var (file, folder) in moves)
{
    string dir = "Assets/Scripts/Gumori/" + folder;
    if (!UnityEditor.AssetDatabase.IsValidFolder(dir)) UnityEditor.AssetDatabase.CreateFolder("Assets/Scripts/Gumori", folder);
    string err = UnityEditor.AssetDatabase.MoveAsset($"Assets/Scripts/Gumori/{file}.cs", $"{dir}/{file}.cs");
    log.AppendLine($"{file} → {folder} {(string.IsNullOrEmpty(err) ? "ok" : err)}");
}
return log.ToString();
