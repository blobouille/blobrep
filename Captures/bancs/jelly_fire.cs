// Play (Space) : un tir à pleine vitesse, direction tournante d'un appel à l'autre (angle d'or).
var l = UnityEngine.Object.FindAnyObjectByType<GumoriLauncher>();
int i = l.Shots.Count;
if (l.Rack.Count == 0) return "réserve vide";
float ang = (i * 137.5f) % 360f;
l.Launch(i % l.Rack.Count, new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad)) * l.maxSpeed);
return "tir " + i;
