// Petite partie scriptée (lâchers alternés gauche / droite) jouée en temps réel avant une capture d'écran.
var d = UnityEngine.Object.FindAnyObjectByType<GumoriDropper>();
uint SP = 1u << (int)GameKey.Space, RI = 1u << (int)GameKey.RightArrow, LE = 1u << (int)GameKey.LeftArrow;
var inputs = new System.Collections.Generic.List<uint>();
for (int i = 0; i < 1400; i++) { uint m = 0; if (i % 50 < 2) m |= SP; m |= ((i / 60) % 2 == 0) ? RI : LE; inputs.Add(m); }
LevelRestart.Restart();
LevelSimulation.FastForward(inputs, inputs.Count);
return "gumori " + d.Count + " score " + d.GetComponent<GumoriScore>().Score;
