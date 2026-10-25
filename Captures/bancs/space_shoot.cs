// Un tir : place et vitesse tirées d'un compteur (reproductible), puis état de la réserve.
var l = UnityEngine.Object.FindAnyObjectByType<BlobLauncher>();
var d = l.GetComponent<BlobDropper>();
int i = l.Shots.Count;
int slot = (i * 7) % l.SlotCount;
float ang = -90f + ((i * 37) % 120 - 60);
float speed = 1.5f + (i * 13) % 9;
var v = new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad)) * speed;
if (l.RackBlob(slot) != null) l.Launch(slot, v);
var sb = new System.Text.StringBuilder();
for (int s = 0; s < l.SlotCount; s++) sb.Append(l.RackBlob(s) != null ? "o" : "_");
return "tirs " + l.Shots.Count + " réserve " + sb + " blobs " + d.Count + " score " + d.GetComponent<SlimeScore>().Score;
