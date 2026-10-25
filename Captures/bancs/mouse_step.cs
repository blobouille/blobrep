// Souris simulée : mouse.txt = "phase dx dy" (phase 0 = appui sur le 1er slime de la réserve, 1 = glisser de (dx,dy) m, 2 = relâcher).
var l = UnityEngine.Object.FindAnyObjectByType<BlobLauncher>();
var cam = Camera.main;
var v = System.IO.File.ReadAllText("Captures/bancs/mouse.txt").Trim().Split(' ');
int phase = int.Parse(v[0]);
float dx = float.Parse(v[1], System.Globalization.CultureInfo.InvariantCulture), dy = float.Parse(v[2], System.Globalization.CultureInfo.InvariantCulture);
if (l.Rack.Count == 0) return "réserve vide";
var b = l.Rack[0];
Vector2 c = Vector2.zero;
for (int k = 0; k < b.CapsuleCount; k++) c += b.Capsule(k).position;
c /= b.CapsuleCount;
Vector2 world = c + new Vector2(dx, dy);
Vector3 s = cam.WorldToScreenPoint(world);
var mouse = UnityEngine.InputSystem.Mouse.current;
var state = new UnityEngine.InputSystem.LowLevel.MouseState { position = new Vector2(s.x, s.y) };
if (phase < 2) state = state.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left, true);
UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, state);
return $"phase {phase} écran ({s.x:F0},{s.y:F0}) / {Screen.width}x{Screen.height} tirs {l.Shots.Count} réserve {l.Rack.Count} pointer {UnityEngine.InputSystem.Pointer.current?.name}";
