// Scène « bombes (essai) » : paroi cassable, bombes posées et dans la réserve, habillage des bombes ; puis sauvegarde.
var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if (!scene.path.EndsWith("bombes (essai).unity")) return "pas la bonne scène : " + scene.path;
var wall = UnityEngine.GameObject.Find("Réservoir").GetComponent<CurveCollider2D>();
wall.breakable = true; wall.pieceLength = 1f; wall.pieceHits = 2;
var jeu = UnityEngine.GameObject.Find("Jeu").transform;
var meca = jeu.Find("Mécaniques");
if (meca == null) { meca = new UnityEngine.GameObject("Mécaniques").transform; meca.SetParent(jeu, false); meca.SetSiblingIndex(jeu.Find("Corps").GetSiblingIndex() + 1); }
meca.position = UnityEngine.Vector3.zero;
var bombs = meca.GetComponent<GumoriBombs>() ?? meca.gameObject.AddComponent<GumoriBombs>();
bombs.dropper = jeu.GetComponent<GumoriDropper>();
bombs.placed.Clear();
bombs.placed.Add(new GumoriBombs.Placed { position = new UnityEngine.Vector2(-2.6f, 0.2f), level = 1 });
bombs.placed.Add(new GumoriBombs.Placed { position = new UnityEngine.Vector2(2.4f, 0.6f), level = 3 });
bombs.inRack.Clear();
bombs.inRack.Add(new GumoriBombs.InRack { arrival = 5, level = 0 });
bombs.inRack.Add(new GumoriBombs.InRack { arrival = 10, level = 2 });
var hab = UnityEngine.GameObject.Find("Habillage");
if (hab.GetComponent<GumoriBombArt>() == null) hab.AddComponent<GumoriBombArt>();
UnityEditor.EditorUtility.SetDirty(wall); UnityEditor.EditorUtility.SetDirty(bombs);
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
return "ok, max lâché " + bombs.dropper.maxDropLevel;
