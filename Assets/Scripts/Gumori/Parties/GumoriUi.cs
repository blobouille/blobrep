using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Interface OnGUI commune (réservoir, carte) : taille selon l'écran, textes cernés, boutons en gelée arrondis, étoiles.
// Textures calculées au premier usage (aucune texture importée). À appeler seulement depuis OnGUI.
public static class GumoriUi
{
    static Texture2D _round, _roundSmall, _starOn, _starOff;
    static GUIStyle _panel, _panelSmall, _text;

    // Unité de taille : 1 = écran de 540 × 960 px.
    public static float Unit => Mathf.Max(0.75f, Mathf.Min(Screen.width / 540f, Screen.height / 960f));

    public static readonly Color Ink = new Color(0.08f, 0.04f, 0.12f);
    public static readonly Color Gold = new Color(1f, 0.82f, 0.25f);
    public static readonly Color Mint = new Color(0.35f, 0.85f, 0.55f);
    public static readonly Color Pink = new Color(1f, 0.45f, 0.7f);
    public static readonly Color Sky = new Color(0.35f, 0.7f, 1f);

    static void Prepare()
    {
        if (_round != null) return;
        _round = RoundTexture(64, 30);
        _roundSmall = RoundTexture(32, 13);
        _starOn = StarTexture(96, true);
        _starOff = StarTexture(96, false);
        _panel = new GUIStyle { normal = { background = _round }, border = new RectOffset(30, 30, 30, 30) };
        _panelSmall = new GUIStyle { normal = { background = _roundSmall }, border = new RectOffset(13, 13, 13, 13) };
        _text = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false, clipping = TextClipping.Overflow };
    }

    // Texte cerné de sombre ; 'size' en unités (multiplié par Unit). 'rich' : même texte avec balises de couleur pour le dessus.
    public static void Label(Rect r, string text, float size, Color color, float alpha = 1f, TextAnchor anchor = TextAnchor.MiddleCenter, string rich = null)
    {
        if (alpha <= 0f) return;
        Prepare();
        _text.fontSize = Mathf.Max(6, Mathf.RoundToInt(size * Unit));
        _text.alignment = anchor;
        _text.richText = false;
        float o = Mathf.Max(1.5f, 0.08f * size * Unit);
        _text.normal.textColor = new Color(Ink.r, Ink.g, Ink.b, alpha * 0.9f);
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.PI / 4f;
            GUI.Label(new Rect(r.x + Mathf.Cos(a) * o, r.y + Mathf.Sin(a) * o + o * 0.5f, r.width, r.height), text, _text);
        }
        color.a *= alpha;
        _text.normal.textColor = color;
        _text.richText = rich != null;
        GUI.Label(r, rich ?? text, _text);
        _text.richText = false;
    }

    // Texte cerné de sombre avec un style donné, tailles en pixels (HUD du réservoir, annonces) : contour de 'width' px,
    // ombre décalée de 'drop' px vers le bas, d'opacité 'shadow'.
    public static void Outlined(Rect r, string text, GUIStyle style, Color color, float alpha, float width = 2f, float drop = 1f, float shadow = 0.9f)
    {
        style.normal.textColor = new Color(Ink.r, Ink.g, Ink.b, alpha * shadow);
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.PI / 4f;
            GUI.Label(new Rect(r.x + Mathf.Cos(a) * width, r.y + Mathf.Sin(a) * width + drop, r.width, r.height), text, style);
        }
        color.a *= alpha;
        style.normal.textColor = color;
        GUI.Label(r, text, style);
    }

    // Rectangle aux coins très arrondis, teinté.
    public static void Panel(Rect r, Color color)
    {
        Prepare();
        if (Event.current.type != EventType.Repaint) return;
        var saved = GUI.color;
        GUI.color = color;
        // Petits rectangles : coins plus petits (le découpage en 9 du grand arrondi ne tient pas sous 60 px).
        (Mathf.Min(r.width, r.height) < 62f ? _panelSmall : _panel).Draw(r, false, false, false, false);
        GUI.color = saved;
    }

    // Bouton en gelée (ombre, corps, reflet) ; enfoncé tant qu'on appuie dessus. Vrai au relâchement.
    public static bool Button(Rect r, string text, Color color, float size = 26f, float alpha = 1f)
    {
        Prepare();
        float u = Unit;
        bool over = r.Contains(Event.current.mousePosition);
        bool pressed = over && PointerDown();
        float drop = pressed ? 2f * u : 0f;
        var body = new Rect(r.x, r.y + drop, r.width, r.height - 5f * u);
        Panel(new Rect(r.x, r.y + 5f * u, r.width, r.height - 5f * u), new Color(color.r * 0.55f, color.g * 0.45f, color.b * 0.55f, alpha));
        Panel(body, new Color(color.r, color.g, color.b, alpha));
        Panel(new Rect(body.x + 0.12f * body.height, body.y + 0.08f * body.height, body.width - 0.24f * body.height, body.height * 0.42f), new Color(1f, 1f, 1f, 0.3f * alpha));
        Label(body, text, size, Color.white, alpha);
        return alpha > 0.5f && GUI.Button(r, GUIContent.none, GUIStyle.none);
    }

    public static void Star(Rect r, bool filled, float alpha = 1f)
    {
        Prepare();
        if (alpha <= 0f || Event.current.type != EventType.Repaint) return;
        var saved = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, alpha);
        GUI.DrawTexture(r, filled ? _starOn : _starOff);
        GUI.color = saved;
    }

    // Voile sombre sur tout l'écran.
    public static void Dim(float alpha)
    {
        if (alpha <= 0f || Event.current.type != EventType.Repaint) return;
        var saved = GUI.color;
        GUI.color = new Color(0.03f, 0.01f, 0.08f, alpha);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = saved;
    }

    // Rebond d'apparition : 0 → 1 avec un léger dépassement.
    public static float Pop(float t, float duration = 0.35f)
    {
        float x = Mathf.Clamp01(t / duration);
        if (x >= 1f) return 1f;
        const float c = 1.70158f * 1.4f;
        x -= 1f;
        return 1f + (c + 1f) * x * x * x + c * x * x;
    }

    public static bool PointerDown()
    {
#if ENABLE_INPUT_SYSTEM
        var p = Pointer.current;
        return p != null && p.press.isPressed;
#else
        return Input.GetMouseButton(0);
#endif
    }

    // ---- Textures ----

    static Texture2D RoundTexture(int size, float radius)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color32[size * size];
        float h = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float qx = Mathf.Abs(x + 0.5f - h) - (h - radius), qy = Mathf.Abs(y + 0.5f - h) - (h - radius);
                float d = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                px[y * size + x] = new Color32(255, 255, 255, (byte)(255f * Mathf.Clamp01(0.5f - d)));
            }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return tex;
    }

    static Texture2D StarTexture(int size, bool filled)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color32[size * size];
        float pixel = 2f / size;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2((x + 0.5f) / size * 2f - 1f, (y + 0.5f) / size * 2f - 1f + 0.06f);
                float d = StarSdf(p, 0.82f, 0.48f) - 0.07f;
                float edge = Mathf.Clamp01(0.5f - (d - 0.07f) / pixel);
                float inner = Mathf.Clamp01(0.5f - (d + 0.03f) / pixel);
                Color fill = filled ? Color.Lerp(new Color(1f, 0.62f, 0.08f), new Color(1f, 0.95f, 0.5f), Mathf.Clamp01(p.y * 0.6f + 0.55f))
                                    : new Color(0.25f, 0.2f, 0.35f, 0.75f);
                Color line = filled ? new Color(0.5f, 0.22f, 0.04f) : new Color(0.1f, 0.06f, 0.16f, 0.8f);
                Color c = Color.Lerp(line, fill, inner);
                c.a *= edge;
                px[y * size + x] = c;
            }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return tex;
    }

    // Étoile à 5 branches (distance signée, Inigo Quilez) : rayon 'r', creux 'rf'.
    static float StarSdf(Vector2 p, float r, float rf)
    {
        var k1 = new Vector2(0.809016994f, -0.587785252f);
        var k2 = new Vector2(-k1.x, k1.y);
        p.x = Mathf.Abs(p.x);
        p -= 2f * Mathf.Max(Vector2.Dot(k1, p), 0f) * k1;
        p -= 2f * Mathf.Max(Vector2.Dot(k2, p), 0f) * k2;
        p.x = Mathf.Abs(p.x);
        p.y -= r;
        Vector2 ba = rf * new Vector2(-k1.y, k1.x) - new Vector2(0f, 1f);
        float h = Mathf.Clamp(Vector2.Dot(p, ba) / Vector2.Dot(ba, ba), 0f, r);
        return (p - ba * h).magnitude * Mathf.Sign(p.y * ba.x - p.x * ba.y);
    }
}
