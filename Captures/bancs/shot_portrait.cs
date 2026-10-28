// Play : rendu de la caméra principale en format téléphone portrait (1080 × 2340) → Captures/niveau/portrait.png.
var cam = Camera.main;
var rt = new RenderTexture(540, 1170, 24);
var old = cam.targetTexture;
cam.targetTexture = rt; cam.aspect = 540f / 1170f;
cam.Render();
RenderTexture.active = rt;
var tex = new Texture2D(540, 1170, TextureFormat.RGB24, false);
tex.ReadPixels(new Rect(0, 0, 540, 1170), 0, 0); tex.Apply();
System.IO.File.WriteAllBytes("Captures/niveau/portrait.png", tex.EncodeToPNG());
RenderTexture.active = null; cam.targetTexture = old; cam.ResetAspect(); rt.Release();
return "taille ortho " + cam.orthographicSize;
