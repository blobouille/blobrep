// À chaque naissance de fusion : surface du slime né (polygone des capsules) / surface cible finale de GumoriFoam.
var l = UnityEngine.Object.FindAnyObjectByType<GumoriLauncher>();
var d = l.GetComponent<GumoriDropper>();
var f = d.GetComponent<GumoriFusion>();
var foam = d.GetComponent<GumoriFoam>();
d.seed = 2;
LevelRestart.Restart();
float Area(Gumori b) { int n = b.CapsuleCount; float a = 0f; for (int k = 0; k < n; k++) { Vector2 p = b.Capsule(k).position, q = b.Capsule((k + 1) % n).position; a += p.x * q.y - q.x * p.y; } return 0.5f * a; }
float Target(Gumori b) { int n = b.CapsuleCount; float r = b.RestRingRadius; return foam.inflate * 0.5f * n * r * r * Mathf.Sin(2f * Mathf.PI / n); }
var born = new System.Collections.Generic.List<Gumori>();
var parents = new System.Collections.Generic.List<float>();
var sb = new System.Text.StringBuilder();
System.Action<Gumori, int, Vector2> onMerge = (b, lv, at) => { sb.Append($"niv{lv}: {Area(b) / Target(b):F2} "); };
f.Merged += onMerge;
var one = new System.Collections.Generic.List<uint> { 0u };
while (LevelSimulation.Step < 900)
{
    int s = LevelSimulation.Step;
    if (s % 40 == 39 && l.Rack.Count > 0)
    {
        int i = s / 40;
        float ang = -90f + ((i * 37) % 140 - 70);
        float speed = 2f + (i * 13) % 4;
        l.Launch((i * 7) % l.Rack.Count, new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad)) * speed);
    }
    LevelSimulation.FastForward(one, 1);
}
f.Merged -= onMerge;
float pr = 0f, cnt = 0f;
foreach (var b in d.InPlay) { pr += Area(b) / Target(b); cnt++; }
return $"surface à la naissance / cible : {sb}| slimes posés en moyenne : {pr / cnt:F2}";
