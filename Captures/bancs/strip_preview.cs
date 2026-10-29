// Hors Play : bandeau des stades (GumoriLevelStrip) rendu en portrait de téléphone (540 × 1170), réglages lus dans
// Captures/bancs/strip_preview.txt : "contraste alignement(0 Centre,1 Sol,2 Plafond) écart nom". Réglages de la scène remis après.
var args = System.IO.File.ReadAllText("Captures/bancs/strip_preview.txt").Trim().Split(' ');
var strip = UnityEngine.Object.FindAnyObjectByType<GumoriLevelStrip>();
float c0 = strip.sizeContrast, g0 = strip.gap; var a0 = strip.align;
strip.sizeContrast = float.Parse(args[0], System.Globalization.CultureInfo.InvariantCulture);
strip.align = (GumoriLevelStrip.Align)int.Parse(args[1]);
strip.gap = float.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture);
var skin0 = UnityEngine.Object.FindAnyObjectByType<GumoriSkin>(); bool gal0 = skin0.gallery; skin0.gallery = false;
var cam = Camera.main;
var rt = new RenderTexture(540, 1170, 24);
var old = cam.targetTexture;
cam.targetTexture = rt; cam.aspect = 540f / 1170f;
var f = typeof(GumoriSkin).GetField("_builtFrame", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
f.SetValue(UnityEngine.Object.FindAnyObjectByType<GumoriSkin>(), -1);
cam.Render();
RenderTexture.active = rt;
var tex = new Texture2D(540, 1170, TextureFormat.RGB24, false);
tex.ReadPixels(new Rect(0, 0, 540, 1170), 0, 0); tex.Apply();
System.IO.File.WriteAllBytes("Captures/niveau/bandeau_" + args[3] + ".png", tex.EncodeToPNG());
RenderTexture.active = null; cam.targetTexture = old; cam.ResetAspect(); rt.Release();
skin0.gallery = gal0;
strip.sizeContrast = c0; strip.align = a0; strip.gap = g0;
return "ok";
