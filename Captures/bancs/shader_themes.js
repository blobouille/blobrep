// Nouveaux éléments du shader Slime/Body : bulles soignées, eau, cactus, filets de chocolat, braises, museau (outil ponctuel).
const fs = require('fs');
const path = require('path');
const file = path.join(__dirname, '..', '..', 'Assets', 'Shaders', 'SlimeBody.shader');
let s = fs.readFileSync(file, 'utf8');
const rep = (x, y) => { if (!s.includes(x)) throw new Error('missing: ' + x.slice(0, 90)); s = s.split(x).join(y); };

// Bulles : fonction commune (motif bulles et motif eau), vraies bulles de verre qui ondulent et grossissent en montant.
rep(`            // Motif dans le repère du décor. Renvoie la couleur modifiée.`, String.raw`            // Bulles de verre qui montent en ondulant et grossissent : liseré, voile teinté, deux reflets.
            float3 Bubbles(float2 p, float3 col, float3 accent, float seed, float t, float faceMask, int count, float amount)
            {
                for (int i = 0; i < count; i++)
                {
                    float h = Hash21(float2(i, seed * 7.0));
                    float h2 = Hash21(float2(seed, i + 3.1));
                    float y = frac(h2 + t * (0.05 + 0.06 * h)) * 2.2 - 1.1;
                    float rise = (y + 1.1) / 2.2;
                    float rad = (0.045 + 0.085 * frac(h * 13.0)) * (0.75 + 0.4 * rise);
                    float2 c = float2(h * 1.3 - 0.65 + 0.05 * sin(t * 2.0 + h * 20.0), y);
                    float d = length(p - c);
                    float fade = smoothstep(1.05, 0.7, abs(y)) * smoothstep(0.95, 0.7, length(c)) * lerp(0.45, 1.0, faceMask) * amount;
                    float inside = Fill(d - rad);
                    float ring = Fill(abs(d - rad) - 0.011);
                    float hl = Fill(length(p - c - rad * float2(-0.38, 0.38)) - rad * 0.3);
                    float hl2 = Fill(length(p - c - rad * float2(0.4, -0.38)) - rad * 0.12);
                    col = lerp(col, lerp(col, accent, 0.22), inside * fade);
                    col = lerp(col, accent * 1.1, saturate(ring * 0.8 + hl + hl2 * 0.7) * fade);
                }
                return col;
            }

            // Motif dans le repère du décor. Renvoie la couleur modifiée.`);

rep(`                if (pattern == 1) // bulles qui montent
                {
                    for (int i = 0; i < 6; i++)
                    {
                        float h = Hash21(float2(i, seed * 7.0));
                        float2 c = float2(h * 1.3 - 0.65, frac(Hash21(float2(seed, i)) + t * (0.06 + 0.05 * h)) * 2.0 - 1.0);
                        float rad = 0.05 + 0.06 * frac(h * 13.0);
                        float d = length(p - c);
                        float fade = smoothstep(1.0, 0.6, abs(c.y)) * smoothstep(0.95, 0.7, length(c));
                        float ring = Fill(abs(d - rad) - 0.014) * 0.7 + Fill(d - rad) * 0.15 + Fill(length(p - c - rad * float2(-0.35, 0.35)) - rad * 0.3);
                        col = lerp(col, accent, saturate(ring) * fade * 0.8);
                    }
                }`, `                if (pattern == 1) // bulles qui montent
                {
                    col = Bubbles(p, col, accent, seed, t, faceMask, 7, 0.9);
                }`);

rep(`                return col;
            }

            // Reflet par-dessus tout le gumori`, String.raw`                else if (pattern == 13) // eau : reflets de vagues qui ondulent (caustiques), plus de bulles
                {
                    float2 q = p * 2.8 + seed;
                    q += 0.25 * float2(sin(q.y * 2.1 + t * 0.9), sin(q.x * 1.8 - t * 0.7));
                    float3 v = Voronoi(q);
                    float caustic = 1.0 - smoothstep(0.0, 0.1, v.y);
                    float depth = smoothstep(-0.9, 0.7, p.y);
                    col = lerp(col, accent, caustic * 0.32 * depth * lerp(0.4, 1.0, faceMask));
                    col = Bubbles(p, col, accent, seed, t, faceMask, 10, 1.0);
                }
                else if (pattern == 14) // cactus : côtes verticales (creux plus sombres) et petites épines blanches sur les côtes
                {
                    float sy = sqrt(max(1.0 - p.y * p.y, 0.04));
                    float u = p.x / sy * 2.5;
                    float fu = frac(u + 0.5) - 0.5;
                    float rib = floor(u + 0.5);
                    float k = lerp(0.4, 1.0, faceMask);
                    col = lerp(col, shade, smoothstep(0.32, 0.5, abs(fu)) * 0.32 * k);
                    col = lerp(col, col * 1.12 + 0.03, smoothstep(0.18, 0.0, abs(fu)) * 0.35 * k);
                    float2 sp = float2(fu * sy / 2.5, (frac(p.y * 3.2 + Hash21(float2(rib, seed))) - 0.5) / 3.2);
                    float ds = min(SdSegment(sp, float2(-0.024, -0.014), float2(0.024, 0.014)), SdSegment(sp, float2(-0.024, 0.014), float2(0.024, -0.014)));
                    ds = min(ds, SdSegment(sp, float2(0.0, -0.026), float2(0.0, 0.026)));
                    float on = smoothstep(0.92, 0.75, length(p));
                    col = lerp(col, accent, Fill(ds - 0.006) * on * k);
                }
                else if (pattern == 15) // filets de chocolat en zigzag (haut et bas, pas sur le visage)
                {
                    float d = 10.0;
                    for (int i = 0; i < 4; i++)
                    {
                        float y0 = i < 2 ? -0.72 + 0.24 * i : 0.4 + 0.24 * (i - 2);
                        float zig = abs(frac(p.x * 2.4 + i * 0.37 + seed) - 0.5) * 2.0 - 0.5;
                        d = min(d, abs(p.y - (y0 + zig * 0.13)) / 1.2);
                    }
                    float on = smoothstep(0.93, 0.8, length(p));
                    col = lerp(col, accent, Fill(d - 0.028) * on * lerp(0.25, 1.0, faceMask));
                }
                return col;
            }

            // Reflet par-dessus tout le gumori`);

// Braises : reflet n° 3, couleur du motif.
rep(`            float3 Sheen(int sheen, float2 p, float3 col, float seed, float faceMask, float t)`,
    `            float3 Sheen(int sheen, float2 p, float3 col, float3 accent, float seed, float faceMask, float t)`);
rep(`                    col += float3(1.2, 1.2, 1.2) * g * lerp(0.5, 1.0, faceMask);
                }
                return col;`, String.raw`                    col += float3(1.2, 1.2, 1.2) * g * lerp(0.5, 1.0, faceMask);
                }
                else if (sheen == 3) // braises qui s'envolent en vacillant
                {
                    float e = 0.0;
                    for (int i = 0; i < 2; i++)
                    {
                        float2 g = float2(p.x * 6.0 + i * 3.7, p.y * 6.0 - t * (1.2 + 0.5 * i) + seed);
                        float2 cell = floor(g);
                        float h = Hash21(cell + i * 11.0);
                        float2 q = frac(g) - 0.5 - 0.3 * (Hash22(cell + 4.2) - 0.5);
                        q.x += 0.15 * sin(t * 3.0 + h * 30.0);
                        float flick = 0.6 + 0.4 * sin(t * 9.0 + h * 50.0);
                        e += step(h, 0.35) * exp(-dot(q, q) * 160.0) * flick;
                    }
                    col += accent * e * 2.0 * lerp(0.4, 1.0, faceMask);
                }
                return col;`);
rep(`col = Sheen((int)(code / 32u), p, col, seed, faceMask, t);`, `col = Sheen((int)(code / 32u), p, col, accent, seed, faceMask, t);`);

// Museau (bouche n° 5) : ovale clair, petite truffe, bouche de chat dessous. Dessiné sous la bouche, pour toutes les expressions.
rep(`                float2 m = q - float2(0.0, -0.16);
                float mouth = 0.0, tongue = 0.0, fang = 0.0;`, String.raw`                float2 m = q - float2(0.0, -0.16);
                float mouth = 0.0, tongue = 0.0, fang = 0.0;
                if (mouthStyle == 5)
                {
                    col = lerp(col, lerp(col, float3(1.0, 0.86, 0.68), 0.75), Fill(SdEllipse(m - float2(0.0, -0.01), float2(0.2, 0.135))));
                    col = lerp(col, ink, Fill(SdEllipse(m - float2(0.0, 0.055), float2(0.055, 0.038))));
                    m.y += 0.045;
                }`);
rep(`                else if (mouthStyle == 1)
                {`, `                else if (mouthStyle == 1 || mouthStyle == 5)
                {`);

fs.writeFileSync(file, s);
console.log('ok');
