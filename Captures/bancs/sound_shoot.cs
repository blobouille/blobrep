// Un tir reproductible depuis la réserve (compteur de tirs), pour écouter / mesurer le son.
var l = UnityEngine.Object.FindAnyObjectByType<GumoriLauncher>();
int i = l.Shots.Count;
if (l.Rack.Count == 0) return "réserve vide";
int slot = (i * 7) % l.Rack.Count;
float ang = -90f + ((i * 37) % 100 - 50);
float speed = l.maxSpeed * (0.45f + 0.55f * ((i * 13) % 7) / 6f);
l.Launch(slot, new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad)) * speed);
return "tir " + i + " vitesse " + (speed * 3.6f).ToString("0") + " km/h";
