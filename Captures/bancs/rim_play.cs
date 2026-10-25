var l = UnityEngine.Object.FindAnyObjectByType<BlobLauncher>();
var one = new System.Collections.Generic.List<uint> { 0u };
int shots = 0;
for (int s = 0; s < 420; s++)
{
    if (s % 40 == 10 && l != null && l.Rack.Count > 0)
    {
        float ang = -90f + ((shots * 37) % 100 - 50);
        l.Launch(shots % l.Rack.Count, new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad)) * 3f);
        shots++;
    }
    LevelSimulation.FastForward(one, 1);
}
var d = UnityEngine.Object.FindAnyObjectByType<BlobDropper>();
return "tirs " + shots + " slimes " + d.Count;
