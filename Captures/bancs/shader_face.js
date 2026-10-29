// Réécrit Face() et ajoute Sheen() / Dark() dans Assets/Shaders/SlimeBody.shader (outil ponctuel).
const fs = require('fs');
const path = require('path');
const file = path.join(__dirname, '..', '..', 'Assets', 'Shaders', 'SlimeBody.shader');
let s = fs.readFileSync(file, 'utf8');
const rep = (x, y) => { if (!s.includes(x)) throw new Error('missing: ' + x.slice(0, 80)); s = s.split(x).join(y); };

const a = s.indexOf('            // Visage :'), b = s.indexOf('            float4 frag(');
const face = String.raw`            // Reflet par-dessus tout le gumori : 1 holo (carte brillante : arc-en-ciel en bandes qui passent), 2 étincelles.
            float3 Sheen(int sheen, float2 p, float3 col, float seed, float faceMask, float t)
            {
                if (sheen == 1)
                {
                    float d = dot(p, float2(0.8, 0.6));
                    float sweep = smoothstep(0.55, 1.0, sin(d * 2.4 - t * 1.2 + seed) * 0.5 + 0.5);
                    float lines = 0.7 + 0.3 * sin((p.x - p.y * 0.7) * 42.0);
                    float3 rainbow = Hue(frac(d * 0.8 + p.y * 0.3 + t * 0.08));
                    col += rainbow * (0.1 + 0.4 * sweep) * lines * lerp(0.35, 1.0, faceMask);
                }
                else if (sheen == 2)
                {
                    float g = Glitter(p, 9.0, 0.4, seed + 9.0, t) + Glitter(p + 0.21, 15.0, 0.3, seed + 4.0, t * 1.2) * 0.6;
                    col += float3(1.2, 1.2, 1.2) * g * lerp(0.5, 1.0, faceMask);
                }
                return col;
            }

            // Visage : 'q' en unités du visage (centré, repère du décor / taille du visage). Cases indépendantes : yeux (0 ronds,
            // 1 étoilés, 2 lumineux), bouche (0 sourire, 1 chat, 2 grand sourire, 3 en coin, 4 croc), attitude au repos (0 calme,
            // 1 rêveur, 2 fier, 3 féroce). 'ink' = encre du visage (claire sur un corps très foncé), 'eyeGlow' = yeux lumineux.
            float3 Face(float2 q, int eyes, int mouthStyle, int attitude, int expr, float2 look, float blink, float3 col, float3 ink,
                        float3 eyeGlow, float t, float blushOn)
            {
                if (abs(q.x) > 0.75 || abs(q.y) > 0.6) return col;
                float3 white = float3(1.0, 1.0, 1.0);
                float side = q.x < 0.0 ? -1.0 : 1.0;
                float2 e = float2(abs(q.x), q.y) - float2(0.31, 0.07) - float2(look.x * side, look.y);

                // Joues roses
                if (blushOn > 0.5 || expr == 1)
                {
                    float b = SdEllipse(float2(abs(q.x), q.y) - float2(0.48, -0.11), float2(0.09, 0.045));
                    col = lerp(col, float3(1.0, 0.45, 0.55), saturate(0.5 - b / 0.04) * 0.45);
                }

                float eye = 0.0, glint = 0.0, pupil = 0.0;
                bool closedArc = expr == 1 || (expr == 0 && attitude == 1);
                bool cross = !closedArc && expr == 3;
                if (closedArc)
                {
                    // ^ ^ (heureux) ou ‿ ‿ (rêveur)
                    float2 a = expr == 1 ? e - float2(0.0, -0.06) : float2(e.x, -e.y) - float2(0.0, -0.075);
                    eye = Fill(SdArc(a, 1.0, 0.115, 0.028));
                }
                else if (cross)
                {
                    // > <
                    float2 c = float2(e.x * side, e.y);
                    float d = min(SdSegment(c, float2(-0.09 * side, 0.08), float2(0.08 * side, 0.0)),
                                  SdSegment(c, float2(-0.09 * side, -0.08), float2(0.08 * side, 0.0)));
                    eye = Fill(d - 0.027);
                }
                else
                {
                    float2 r = expr == 2 ? float2(0.155, 0.18) : float2(0.135, 0.165);
                    float open = 1.0 - blink;
                    float2 er = float2(r.x, max(r.y * open, 0.012));
                    float d = SdEllipse(e, er);
                    if (attitude == 2 && expr == 0) d = max(d, e.y - 0.045); // paupières mi-closes (fier)
                    eye = Fill(d);
                    if (open > 0.3)
                    {
                        float2 g = e - float2(-0.045, 0.062) * open;
                        float star = eyes == 1 ? exp(-abs(g.x) * 46.0 - abs(g.y) * 7.0) + exp(-abs(g.y) * 46.0 - abs(g.x) * 7.0) : 0.0;
                        float tw = eyes == 1 ? 0.7 + 0.3 * sin(t * 5.0 + side) : 1.0;
                        glint = saturate(Fill(length(g) - 0.047) + star * tw) + Fill(length(e - float2(0.048, -0.06)) - 0.022);
                        glint *= eye;
                        pupil = Fill(SdEllipse(e - float2(0.0, -0.01), float2(0.045, 0.1 * open))) * eye;
                    }
                }
                if (eyes == 2 && !closedArc && !cross)
                {
                    col = lerp(col, eyeGlow, eye);
                    col = lerp(col, float3(0.06, 0.03, 0.08), pupil);
                }
                else col = lerp(col, ink, eye);
                col = lerp(col, white * 1.2, glint);

                // Sourcils (féroce)
                if (attitude == 3 && expr != 1)
                {
                    float2 c = float2(abs(q.x), q.y);
                    float d = SdSegment(c, float2(0.19, 0.3), float2(0.43, 0.36));
                    col = lerp(col, ink, Fill(d - 0.025));
                }

                // Bouche : expressions d'abord (heureux, surpris, grimace), sinon la bouche choisie.
                float2 m = q - float2(0.0, -0.16);
                float mouth = 0.0, tongue = 0.0, fang = 0.0;
                if (expr == 1 || (expr == 0 && mouthStyle == 2))
                {
                    float d = max(SdEllipse(m - float2(0.0, 0.02), float2(0.13, 0.11)), m.y - 0.02);
                    mouth = Fill(d);
                    tongue = Fill(length(m - float2(0.0, -0.11)) - 0.06) * mouth;
                }
                else if (expr == 2)
                {
                    float d = SdEllipse(m - float2(0.0, -0.03), float2(0.06, 0.075));
                    mouth = Fill(d);
                    tongue = Fill(length(m - float2(0.0, -0.1)) - 0.045) * mouth;
                }
                else if (expr == 3)
                {
                    float y = 0.02 * sin(m.x * 60.0);
                    mouth = Fill(max(abs(m.y - y) - 0.016, abs(m.x) - 0.1));
                }
                else if (mouthStyle == 1)
                {
                    float2 w = float2(abs(m.x) - 0.045, m.y + 0.01);
                    mouth = Fill(SdArc(float2(w.x, -w.y), 1.2, 0.045, 0.016));
                }
                else if (mouthStyle == 3)
                {
                    float2 w = m - float2(0.03, 0.02);
                    mouth = Fill(SdArc(float2(w.x, -w.y), 0.8, 0.09, 0.018));
                }
                else
                {
                    float2 w = m - float2(0.0, 0.08);
                    mouth = Fill(SdArc(float2(w.x, -w.y), 0.75, 0.12, 0.02));
                    if (mouthStyle == 4) // petit croc qui dépasse, effilé vers le bas
                    {
                        float k = saturate((-m.y - 0.035) / 0.045);
                        fang = Fill(SdSegment(m, float2(0.055, -0.035), float2(0.06, -0.08)) - lerp(0.022, 0.006, k));
                    }
                }
                col = lerp(col, ink, mouth);
                col = lerp(col, float3(1.0, 0.42, 0.5), tongue);
                col = lerp(col, white * 1.1, fang);
                return col;
            }

`;
s = s.slice(0, a) + face + s.slice(b);

rep(`                    rim = lerp(rim, lerp(body, 1.0, 0.45) * 1.15, pow(lit, 4.0) * 0.7);
                    rim = lerp(rim, float3(1.6, 1.6, 1.6), flash);`,
`                    rim = lerp(rim, lerp(body, 1.0, 0.45) * 1.15, pow(lit, 4.0) * 0.7);
                    rim = lerp(rim, lerp(accent, float3(0.42, 0.38, 0.58), 0.65), Dark(body)); // sinon invisible sur fond nuit
                    rim = lerp(rim, float3(1.6, 1.6, 1.6), flash);`);
rep(`                int pattern = (int)round(i.light.w);`, `                int code = (int)round(i.light.w);
                int pattern = code % 32;`);
rep(`                col = Face(fq, (int)round(i.shade.w), (int)round(i.face.w), i.face.xy, i.face.z, col, shade, t, i.misc.z);`,
`                int faceCode = (int)round(i.shade.w);
                float3 ink = lerp(float3(0.12, 0.06, 0.10), lerp(body, float3(1.0, 0.96, 1.0), 0.85), Dark(body));
                col = Face(fq, faceCode % 8, (faceCode / 8) % 8, faceCode / 64, (int)round(i.face.w), i.face.xy, i.face.z, col, ink,
                           accent * (1.0 + glow * 0.6), t, i.misc.z);
                col = Sheen(code / 32, p, col, seed, faceMask, t);`);
rep(`            float SdEllipse(`, `            // 1 pour un corps très foncé (noir), 0 sinon : son visage et son contour passent au clair.
            float Dark(float3 body)
            {
                return 1.0 - smoothstep(0.025, 0.05, dot(body, float3(0.2126, 0.7152, 0.0722)));
            }

            float SdEllipse(`);
fs.writeFileSync(file, s);
console.log('ok');
