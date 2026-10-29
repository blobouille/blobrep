// Un lâcher en jeu réel (un pas avec Espace, décalé à gauche / droite selon le compteur), pour mesurer le son.
var d = UnityEngine.Object.FindAnyObjectByType<GumoriDropper>();
uint SP = 1u << (int)GameKey.Space, RI = 1u << (int)GameKey.RightArrow, LE = 1u << (int)GameKey.LeftArrow;
var inputs = new System.Collections.Generic.List<uint>();
int k = d.Drops;
for (int i = 0; i < 12; i++) inputs.Add(k % 3 == 0 ? RI : k % 3 == 1 ? LE : 0u);
inputs.Add(SP);
LevelSimulation.FastForward(inputs, inputs.Count);
return "lâchers " + d.Drops + " gumori " + d.Count;
