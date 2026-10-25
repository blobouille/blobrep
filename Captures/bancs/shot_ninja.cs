// Gros plans des Ninjakura (+ essai des mêmes éléments sur un corps noir et un corps crème, et sur un gros) → Captures/ninjakura.png.
var skin = UnityEngine.Object.FindAnyObjectByType<GumoriSkin>();
var t = typeof(GumoriSkin);
var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
skin.variants = GumoriDesign.Variants();
var black = GumoriDesign.Variants().Find(v => v.name == "Ninjakura en fleurs");
black.name = "Essai noir"; black.family = GumoriFamily.Black; black.rarity = GumoriRarity.Rare;
black.body = GumoriDesign.Hex("#2E2638"); black.shade = GumoriDesign.Hex("#100C18");
var cream = GumoriDesign.Variants().Find(v => v.name == "Ninjakura");
cream.name = "Essai crème"; cream.family = GumoriFamily.Cream; cream.rarity = GumoriRarity.Rare;
cream.body = GumoriDesign.Hex("#FFEFF3"); cream.shade = GumoriDesign.Hex("#E0BFC8");
skin.variants.Add(black); skin.variants.Add(cream);
skin.galleryVariants = true;
t.GetField("_builtFrame", flags).SetValue(skin, -1);
t.GetMethod("BuildGallery", flags).Invoke(skin, null);
var list = (System.Collections.IList)t.GetField("_gallery", flags).GetValue(skin);
var back = UnityEngine.Object.FindAnyObjectByType<GumoriBackdrop>();
if (back != null) back.enabled = false;
var picks = new System.Collections.Generic.List<object>();
foreach (var e in list) { var d = (GumoriDesign)e.GetType().GetField("design").GetValue(e); if (d.name.StartsWith("Ninja") || d.name.StartsWith("Essai")) picks.Add(e); }
int n = picks.Count, cell = 600;
var sheet = new Texture2D(n * cell, cell, TextureFormat.RGB24, false);
var go = new GameObject("capture ninja") { hideFlags = HideFlags.HideAndDontSave };
var cam = go.AddComponent<Camera>();
cam.orthographic = true;
cam.clearFlags = CameraClearFlags.SolidColor;
cam.backgroundColor = new Color(0.03f, 0.03f, 0.07f);
var rt = new RenderTexture(cell, cell, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
cam.targetTexture = rt;
var tile = new Texture2D(cell, cell, TextureFormat.RGB24, false);
var names = new System.Text.StringBuilder();
for (int i = 0; i < n; i++)
{
    var e = picks[i];
    var et = e.GetType();
    var center = (Vector2)et.GetField("center").GetValue(e);
    float radius = (float)et.GetField("radius").GetValue(e);
    cam.orthographicSize = radius * 1.7f;
    cam.transform.position = new Vector3(center.x, center.y + radius * 0.2f, -10f);
    t.GetField("_builtFrame", flags).SetValue(skin, -1);
    cam.Render();
    var prev = RenderTexture.active;
    RenderTexture.active = rt;
    tile.ReadPixels(new Rect(0, 0, cell, cell), 0, 0);
    tile.Apply();
    RenderTexture.active = prev;
    sheet.SetPixels(i * cell, 0, cell, cell, tile.GetPixels());
    names.Append(((GumoriDesign)et.GetField("design").GetValue(e)).name).Append(" | ");
}
sheet.Apply();
System.IO.File.WriteAllBytes("Captures/ninjakura.png", sheet.EncodeToPNG());
cam.targetTexture = null;
UnityEngine.Object.DestroyImmediate(go);
UnityEngine.Object.DestroyImmediate(rt);
UnityEngine.Object.DestroyImmediate(tile);
UnityEngine.Object.DestroyImmediate(sheet);
if (back != null) back.enabled = true;
skin.variants = GumoriDesign.Variants();
return names.ToString();
