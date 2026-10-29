// Play (scène Space) : tire le gumori de la réserve 'slot' dans la direction (dx, dy) à 'speed' × vitesse max, ralentit le
// temps et le filme une image par pas (Assets/Temp/JellyRec → Captures/gelee/<nom>_NNN.png). Arguments dans jelly_shot.txt :
// "slot dx dy speed nom images taille".
var a = System.IO.File.ReadAllText("Captures/bancs/jelly_shot.txt").Trim().Split(' ');
var inv = System.Globalization.CultureInfo.InvariantCulture;
var l = UnityEngine.Object.FindAnyObjectByType<GumoriLauncher>();
int slot = int.Parse(a[0]);
var blob = slot < l.Rack.Count ? l.Rack[slot] : null;
if (blob == null) return "réserve vide en " + slot;
var dir = new Vector2(float.Parse(a[1], inv), float.Parse(a[2], inv)).normalized;
l.Launch(slot, dir * l.maxSpeed * float.Parse(a[3], inv));
Time.timeScale = 0.25f;
JellyRec.Begin(blob, int.Parse(a[5]), a[4], float.Parse(a[6], inv));
return "tir de " + blob.diameter.ToString("0.00") + " m depuis " + blob.Center;
