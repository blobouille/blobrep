using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Écran titre (Aventure / Sans fin), fiche de niveau et enchaînements de la carte : glisser = faire défiler, toucher un niveau
// débloqué = l'avatar y sautille puis la fiche s'ouvre (« Jouer »). Au retour d'un niveau réussi : la pastille passe au doré et
// ses étoiles apparaissent, les nuages s'écartent, l'avatar saute au niveau suivant et sa fiche s'ouvre. Économise les images
// quand rien ne bouge (OnDemandRendering).
[DisallowMultipleComponent]
public class GumoriMapMenu : MonoBehaviour
{
    public GumoriMap map;
    public GumoriMapAvatar avatar;
    [Tooltip("Temps (s) entre deux images quand rien ne bouge : voir GumoriMap.idleFrameInterval.")]
    [Min(0f)] public float panelPopTime = 0.35f;

    enum State { Title, Map, Panel, Sequence }

    State _state;
    int _panel = -1, _pendingPanel = -1;
    float _stateTime, _toastTime = -10f;
    string _toast;
    bool _pressing, _dragging, _overUi;
    Vector2 _pressPos, _lastPos;
    float _fling;
    readonly List<Rect> _uiRects = new List<Rect>();

    int First => map != null ? map.FirstLevel : 0;
    GumoriCampaign Campaign => map != null ? map.campaign : null;

    void Awake()
    {
        if (map == null || avatar == null) return;
        map.EnsureBuilt();
        int unlocked = map.ShownUnlocked;
        int last = GumoriSession.LastPlayed - First;
        int start = GumoriSession.AvatarLevel >= 0 ? GumoriSession.AvatarLevel - First : last >= 0 && last < map.LevelCount ? last : unlocked - 1;
        avatar.Place(Mathf.Clamp(start, 0, Mathf.Max(0, map.LevelCount - 1)));
    }

    void OnEnable()
    {
        if (avatar != null) avatar.Arrived += OnArrived;
    }

    void OnDisable()
    {
        if (avatar != null) avatar.Arrived -= OnArrived;
        if (Application.isPlaying) OnDemandRendering.renderFrameInterval = 1;
    }

    void Start()
    {
        if (map == null || avatar == null) return;
        map.SnapTo(map.NodeZ(avatar.Level));
        int last = GumoriSession.LastPlayed - First;
        if (last >= 0 && last < map.LevelCount) StartCoroutine(Return(last, GumoriSession.LastWon, GumoriSession.LastUnlocked));
        else if (!GumoriSession.TitleDone) SetState(State.Title);
        else SetState(State.Map);
    }

    void SetState(State s)
    {
        _state = s;
        _stateTime = Time.time;
    }

    // Retour d'un niveau : l'ancienne progression d'abord, puis pastille dorée + étoiles, nuages, saut au niveau suivant.
    IEnumerator Return(int last, bool won, bool unlocked)
    {
        SetState(State.Sequence);
        int real = map.ShownUnlocked;
        if (unlocked) map.ShownUnlocked = real - 1;
        map.SetReveal(map.RevealFor(map.ShownUnlocked));
        if (won) map.PopNode(last, 0.6f);
        GumoriSession.ClearResult();
        yield return new WaitForSeconds(won ? 1.9f : 0.4f);
        if (unlocked && last + 1 < map.LevelCount)
        {
            map.ShownUnlocked = -1;
            map.PopNode(last + 1, 0.9f);
            map.AnimateReveal(map.RevealFor(real), 1.8f);
            yield return new WaitForSeconds(1.4f);
            _pendingPanel = last + 1;
            avatar.GoTo(last + 1);
            SetState(State.Map);
        }
        else
        {
            SetState(State.Map);
            OpenPanel(last);
        }
    }

    void OnArrived(int level)
    {
        GumoriSession.AvatarLevel = First + level;
        if (_pendingPanel == level) OpenPanel(level);
        _pendingPanel = -1;
    }

    void OpenPanel(int level)
    {
        _panel = level;
        SetState(State.Panel);
    }

    void Toast(string text)
    {
        _toast = text;
        _toastTime = Time.time;
    }

    void Update()
    {
        if (map == null || avatar == null) return;
        float dt = Time.deltaTime;
        if (avatar.Moving) map.Follow(map.PathPoint(avatar.PathS).z);
        if (_state == State.Map) HandleInput(dt);
        else { _pressing = false; _fling = 0f; }

        bool busy = _state == State.Title || _state == State.Sequence || avatar.Moving || map.Animating || _pressing
                    || Mathf.Abs(_fling) > 0.01f || Time.time - _stateTime < 0.8f || Time.time - _toastTime < 1.6f;
        OnDemandRendering.renderFrameInterval = busy ? 1 : Mathf.Max(1, map.idleFrameInterval);
    }

    void HandleInput(float dt)
    {
#if ENABLE_INPUT_SYSTEM
        var p = Pointer.current;
        if (p == null) return;
        Vector2 pos = p.position.ReadValue();
        if (p.press.wasPressedThisFrame)
        {
            _pressPos = _lastPos = pos;
            _pressing = true;
            _dragging = false;
            _fling = 0f;
            _overUi = OverUi(pos);
        }
        if (_pressing && p.press.isPressed && !_overUi)
        {
            if (!_dragging && (pos - _pressPos).magnitude > 12f * GumoriUi.Unit) _dragging = true;
            if (_dragging)
            {
                float dz = -(pos.y - _lastPos.y) * map.MetersPerPixel;
                map.Scroll(dz);
                if (dt > 0f) _fling = Mathf.Lerp(_fling, dz / dt, 0.5f);
            }
            _lastPos = pos;
        }
        if (_pressing && p.press.wasReleasedThisFrame)
        {
            _pressing = false;
            if (!_dragging && !_overUi) Tap(pos);
        }
#endif
        if (!_pressing && Mathf.Abs(_fling) > 0.01f)
        {
            map.Scroll(_fling * dt);
            _fling *= Mathf.Exp(-dt * 5f);
        }
    }

    bool OverUi(Vector2 screen)
    {
        var gui = new Vector2(screen.x, Screen.height - screen.y);
        foreach (var r in _uiRects)
            if (r.Contains(gui)) return true;
        return false;
    }

    void Tap(Vector2 screen)
    {
        int node = map.NodeAtScreen(screen);
        if (node < 0) return;
        if (node >= map.ShownUnlocked)
        {
            Toast($"Niveau {First + node + 1} : réussis le niveau {First + map.ShownUnlocked} d'abord !");
            map.PopNode(node);
            return;
        }
        if (!avatar.Moving && avatar.Level == node) { OpenPanel(node); return; }
        _pendingPanel = node;
        avatar.GoTo(node);
    }

    // ---- Interface ----

    void OnGUI()
    {
        if (map == null || avatar == null) return;
        _uiRects.Clear();
        float u = GumoriUi.Unit, w = Screen.width, h = Screen.height;
        float age = Time.time - _stateTime;

        if (_state == State.Title)
        {
            DrawTitle(u, w, h, age);
            return;
        }

        // Haut : zone et étoiles gagnées.
        int total = 0;
        for (int i = 0; i < map.LevelCount; i++) total += GumoriSession.Stars(First + i);
        var zoneChip = new Rect(12f * u, 12f * u, 210f * u, 42f * u);
        GumoriUi.Panel(zoneChip, new Color(1f, 1f, 1f, 0.82f));
        GumoriUi.Label(new Rect(zoneChip.x + 14f * u, zoneChip.y, zoneChip.width - 20f * u, zoneChip.height), $"Zone {map.zone + 1} · {map.ZoneName}", 19f, new Color(0.45f, 0.78f, 0.35f), 1f, TextAnchor.MiddleLeft);
        var starChip = new Rect(w - 160f * u, 12f * u, 148f * u, 42f * u);
        GumoriUi.Panel(starChip, new Color(1f, 1f, 1f, 0.82f));
        GumoriUi.Star(new Rect(starChip.x + 6f * u, starChip.y + 3f * u, 36f * u, 36f * u), true);
        GumoriUi.Label(new Rect(starChip.x + 44f * u, starChip.y, starChip.width - 50f * u, starChip.height), $"{total} / {3 * map.LevelCount}", 19f, GumoriUi.Gold, 1f, TextAnchor.MiddleLeft);

        var menu = new Rect(12f * u, h - 62f * u, 110f * u, 50f * u);
        _uiRects.Add(menu);
        if (_state == State.Map && GumoriUi.Button(menu, "Menu", GumoriUi.Pink, 19f))
        {
            GumoriSession.TitleDone = false;
            SetState(State.Title);
        }

        float toast = Time.time - _toastTime;
        if (toast < 1.6f)
        {
            float a = Mathf.Clamp01((1.6f - toast) * 4f) * Mathf.Clamp01(toast * 8f);
            var r = new Rect(w * 0.5f - 230f * u, h * 0.16f, 460f * u, 46f * u);
            GumoriUi.Panel(r, new Color(0.15f, 0.1f, 0.25f, 0.75f * a));
            GumoriUi.Label(r, _toast, 17f, Color.white, a);
        }

        if (_state == State.Panel) DrawPanel(u, w, h, age);
    }

    void DrawTitle(float u, float w, float h, float age)
    {
        GumoriUi.Dim(0.18f);
        // Titre : un seul texte (espacement naturel des lettres), une couleur de gumori par lettre, qui rebondit.
        const string title = "Slimes";
        _titleRich ??= RichTitle(title);
        float t = Time.time;
        float pop = GumoriUi.Pop(age, 0.6f);
        float bob = Mathf.Sin(t * 2.4f) * 6f * u;
        GumoriUi.Label(new Rect(0f, h * 0.17f + bob - 50f * u, w, 100f * u), title, 84f * pop * (1f + 0.03f * Mathf.Sin(t * 3.1f)), Color.white, Mathf.Clamp01(pop * 2f), TextAnchor.MiddleCenter, _titleRich);
        float a = Mathf.Clamp01((age - 0.5f) * 3f);
        GumoriUi.Label(new Rect(0f, h * 0.17f + 58f * u, w, 30f * u), "fusionne, écrase, fais trembler", 18f, Color.white, a);

        var adventure = new Rect(w * 0.5f - 150f * u, h * 0.66f, 300f * u, 84f * u);
        var endless = new Rect(w * 0.5f - 130f * u, h * 0.66f + 100f * u, 260f * u, 70f * u);
        float b1 = GumoriUi.Pop(age - 0.6f), b2 = GumoriUi.Pop(age - 0.75f);
        if (GumoriUi.Button(Scaled(adventure, b1), "Aventure", GumoriUi.Mint, 32f, Mathf.Clamp01(b1 * 2f)))
        {
            GumoriSession.TitleDone = true;
            SetState(State.Map);
            map.Follow(map.NodeZ(avatar.Level));
        }
        if (GumoriUi.Button(Scaled(endless, b2), "Sans fin", GumoriUi.Sky, 26f, Mathf.Clamp01(b2 * 2f)))
        {
            GumoriSession.TitleDone = true;
            GumoriSession.PlayEndless();
        }
        int best = GumoriSession.EndlessBest;
        if (best > 0) GumoriUi.Label(new Rect(0f, endless.yMax + 6f * u, w, 26f * u), $"record sans fin : {best:N0}", 15f, GumoriUi.Gold, a);
    }

    string _titleRich;

    static string RichTitle(string title)
    {
        Color[] colors = { new Color(0.36f, 0.7f, 1f), new Color(0.5f, 0.88f, 0.3f), new Color(1f, 0.52f, 0.76f), new Color(1f, 0.85f, 0.28f), new Color(0.68f, 0.4f, 0.98f), new Color(1f, 0.55f, 0.15f) };
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < title.Length; i++) sb.Append($"<color=#{ColorUtility.ToHtmlStringRGB(colors[i % colors.Length])}>{title[i]}</color>");
        return sb.ToString();
    }

    static Rect Scaled(Rect r, float s) => new Rect(r.center.x - r.width * s * 0.5f, r.center.y - r.height * s * 0.5f, r.width * s, r.height * s);

    void DrawPanel(float u, float w, float h, float age)
    {
        var campaign = Campaign;
        int global = First + _panel;
        var level = campaign != null ? campaign.Level(global) : null;
        float pop = GumoriUi.Pop(age, panelPopTime);
        GumoriUi.Dim(0.35f * Mathf.Clamp01(age * 4f));
        var card = Scaled(new Rect(w * 0.5f - 190f * u, h * 0.5f - 175f * u, 380f * u, 360f * u), pop);
        _uiRects.Add(new Rect(0f, 0f, w, h));

        // Toucher hors de la fiche la ferme.
        var e = Event.current;
        if (e.type == EventType.MouseUp && !card.Contains(e.mousePosition) && age > 0.3f)
        {
            SetState(State.Map);
            e.Use();
            return;
        }

        GumoriUi.Panel(new Rect(card.x, card.y + 8f * u, card.width, card.height), new Color(0.35f, 0.2f, 0.5f, 0.9f));
        GumoriUi.Panel(card, new Color(1f, 0.97f, 0.92f, 1f));
        var head = new Rect(card.x, card.y, card.width, 78f * u * pop);
        GumoriUi.Panel(head, new Color(0.55f, 0.78f, 1f));
        GumoriUi.Label(head, $"Niveau {global + 1}", 34f * pop, Color.white);

        int stars = GumoriSession.Stars(global);
        for (int i = 0; i < 3; i++)
        {
            float s = (i == 1 ? 64f : 52f) * u * GumoriUi.Pop(age - 0.1f - 0.08f * i);
            float x = card.center.x + (i - 1) * 70f * u, y = card.y + 122f * u * pop - (i == 1 ? 8f * u : 0f);
            GumoriUi.Star(new Rect(x - s * 0.5f, y - s * 0.5f, s, s), i < stars);
        }

        if (level != null)
        {
            string diff = level.difficulty == GumoriGoal.Difficulty.Easy ? "Facile" : level.difficulty == GumoriGoal.Difficulty.Hard ? "Difficile" : "Moyen";
            Color dc = level.difficulty == GumoriGoal.Difficulty.Easy ? GumoriUi.Mint : level.difficulty == GumoriGoal.Difficulty.Hard ? new Color(1f, 0.4f, 0.45f) : GumoriUi.Sky;
            var chip = new Rect(card.center.x - 80f * u, card.y + 168f * u * pop, 160f * u, 34f * u * pop);
            GumoriUi.Panel(chip, dc);
            GumoriUi.Label(chip, diff, 18f * pop, Color.white);
            GumoriUi.Label(new Rect(card.x, card.y + 206f * u * pop, card.width, 32f * u), $"{level.maxDrops} coups", 22f * pop, new Color(0.35f, 0.3f, 0.5f));
            int best = GumoriSession.BestScore(global);
            if (best > 0) GumoriUi.Label(new Rect(card.x, card.y + 236f * u * pop, card.width, 26f * u), $"meilleur : {best:N0}", 15f * pop, new Color(0.85f, 0.6f, 0.15f));
        }

        var play = new Rect(card.center.x - 115f * u, card.yMax - 92f * u * pop, 230f * u, 74f * u * pop);
        if (GumoriUi.Button(play, "Jouer", GumoriUi.Mint, 30f * pop) && age > 0.2f)
        {
            GumoriSession.AvatarLevel = global;
            GumoriSession.PlayLevel(global);
        }
        var close = new Rect(card.xMax - 40f * u, card.y - 12f * u, 52f * u, 52f * u);
        if (GumoriUi.Button(close, "×", GumoriUi.Pink, 26f) && age > 0.2f) SetState(State.Map);
    }
}
