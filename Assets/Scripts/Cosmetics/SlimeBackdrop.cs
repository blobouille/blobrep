using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Décor : fond en dégradé avec bulles floues qui montent (shader Slime/Backdrop), qui pulse à la couleur des fusions (Pulse),
// et bocal de verre dessiné sur la courbe du réservoir (paroi = épaisseur de la courbe, vitre de fond derrière les slimes).
// Purement visuel, dessiné pour chaque caméra.
[ExecuteAlways]
[DisallowMultipleComponent]
[DefaultExecutionOrder(800)]
public class SlimeBackdrop : MonoBehaviour
{
    [Header("Fond")]
    public Shader backdropShader;
    [Tooltip("Couleur du haut de l'écran.")]
    public Color top = new Color(0.17f, 0.11f, 0.34f);
    [Tooltip("Couleur du bas de l'écran.")]
    public Color bottom = new Color(0.04f, 0.13f, 0.22f);
    [Tooltip("Couleur et opacité des bulles floues du fond.")]
    public Color bokeh = new Color(0.55f, 0.8f, 1f, 0.07f);
    [Tooltip("Durée (s) de la pulsation du fond après une fusion.")]
    [Min(0.05f)] public float pulseTime = 0.6f;

    [Header("Bocal")]
    [Tooltip("Courbe du réservoir. Vide = la première de la scène.")]
    public CurvePath2D reservoir;
    public Shader glassShader;
    [Tooltip("Couleur et opacité des parois de verre.")]
    public Color glass = new Color(0.75f, 0.92f, 1f, 0.9f);
    [Tooltip("Couleur et opacité de la vitre de fond (derrière les slimes).")]
    public Color glassBack = new Color(0.55f, 0.8f, 1f, 0.035f);

    Material _backMaterial, _panelMaterial, _glassMaterial;
    Mesh _quad, _walls, _panel;
    Color _pulse;
    float _pulseAmount;
    readonly List<Vector3> _verts = new List<Vector3>();
    readonly List<Vector4> _uv0 = new List<Vector4>(), _uv1 = new List<Vector4>(), _uv2 = new List<Vector4>();
    readonly List<int> _tris = new List<int>();
    readonly List<Vector2> _line = new List<Vector2>();
    readonly Vector3[] _corners = new Vector3[4];
    int _builtFrame = -1;

    static readonly int TopId = Shader.PropertyToID("_Top"), BottomId = Shader.PropertyToID("_Bottom"),
        BokehId = Shader.PropertyToID("_Bokeh"), PulseId = Shader.PropertyToID("_Pulse"), AspectId = Shader.PropertyToID("_Aspect");

    // Le fond s'illumine de cette couleur, puis s'éteint en 'pulseTime'.
    public void Pulse(Color color, float amount)
    {
        _pulse = color;
        _pulseAmount = Mathf.Max(_pulseAmount, amount);
    }

    void OnEnable()
    {
        if (backdropShader == null) backdropShader = Shader.Find("Slime/Backdrop");
        if (glassShader == null) glassShader = Shader.Find("Slime/Fx");
        // Ordre de dessin : fond, vitre de fond, slimes (3000), parois, effets.
        if (backdropShader != null) _backMaterial = new Material(backdropShader) { hideFlags = HideFlags.HideAndDontSave, renderQueue = 2900 };
        if (glassShader != null)
        {
            _panelMaterial = new Material(glassShader) { hideFlags = HideFlags.HideAndDontSave, renderQueue = 2950 };
            _glassMaterial = new Material(glassShader) { hideFlags = HideFlags.HideAndDontSave, renderQueue = 3010 };
        }
        _quad = NewMesh("Fond");
        _quad.vertices = _corners;
        _quad.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
        _quad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        _walls = NewMesh("Parois du bocal");
        _panel = NewMesh("Vitre du bocal");
        RenderPipelineManager.beginCameraRendering += OnBeginCamera;
    }

    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
        DestroyImmediate(_quad);
        DestroyImmediate(_walls);
        DestroyImmediate(_panel);
        if (_backMaterial != null) DestroyImmediate(_backMaterial);
        if (_glassMaterial != null) DestroyImmediate(_glassMaterial);
        if (_panelMaterial != null) DestroyImmediate(_panelMaterial);
    }

    static Mesh NewMesh(string name)
    {
        var mesh = new Mesh { name = name, hideFlags = HideFlags.HideAndDontSave };
        mesh.MarkDynamic();
        return mesh;
    }

    void Update()
    {
        if (Application.isPlaying) _pulseAmount = Mathf.Max(0f, _pulseAmount - Time.unscaledDeltaTime / pulseTime);
    }

    void OnBeginCamera(ScriptableRenderContext context, Camera cam)
    {
        if (cam.cameraType != CameraType.Game && cam.cameraType != CameraType.SceneView) return;
        if (_builtFrame != Time.frameCount)
        {
            _builtFrame = Time.frameCount;
            BuildJar();
        }
        var bounds = new Bounds(Vector3.zero, Vector3.one * 10000f);
        if (_backMaterial != null && cam.orthographic)
        {
            float h = cam.orthographicSize, w = h * cam.aspect;
            Vector3 c = cam.transform.position;
            _corners[0] = new Vector3(c.x - w, c.y - h, 0f);
            _corners[1] = new Vector3(c.x + w, c.y - h, 0f);
            _corners[2] = new Vector3(c.x + w, c.y + h, 0f);
            _corners[3] = new Vector3(c.x - w, c.y + h, 0f);
            _quad.vertices = _corners;
            _quad.bounds = bounds;
            _backMaterial.SetColor(TopId, top);
            _backMaterial.SetColor(BottomId, bottom);
            _backMaterial.SetColor(BokehId, bokeh);
            _backMaterial.SetColor(PulseId, new Color(_pulse.r, _pulse.g, _pulse.b, _pulseAmount * _pulseAmount));
            _backMaterial.SetFloat(AspectId, cam.aspect);
            Graphics.RenderMesh(new RenderParams(_backMaterial) { camera = cam, layer = gameObject.layer, worldBounds = bounds }, _quad, 0, Matrix4x4.identity);
        }
        if (_glassMaterial == null) return;
        if (_panel.vertexCount > 0)
            Graphics.RenderMesh(new RenderParams(_panelMaterial) { camera = cam, layer = gameObject.layer, worldBounds = bounds }, _panel, 0, Matrix4x4.identity);
        if (_walls.vertexCount > 0)
            Graphics.RenderMesh(new RenderParams(_glassMaterial) { camera = cam, layer = gameObject.layer, worldBounds = bounds }, _walls, 0, Matrix4x4.identity);
    }

    // Parois : bande de l'épaisseur de la courbe autour de sa ligne centrale ; vitre : l'intérieur du U, fermé en haut.
    void BuildJar()
    {
        if (reservoir == null) reservoir = FindAnyObjectByType<CurvePath2D>();
        _walls.Clear();
        _panel.Clear();
        if (reservoir == null) return;
        if (reservoir.center.Count < 2) reservoir.Rebuild();
        var center = reservoir.center;
        if (center.Count < 2) return;
        var tr = reservoir.transform;
        _line.Clear();
        for (int i = 0; i < center.Count; i++) _line.Add(tr.TransformPoint(center[i]));
        int n = _line.Count;
        float half = 0.5f * reservoir.thickness;

        Clear();
        float along = 0f;
        for (int i = 0; i < n; i++)
        {
            Vector2 t = _line[Mathf.Min(n - 1, i + 1)] - _line[Mathf.Max(0, i - 1)];
            Vector2 nor = new Vector2(-t.y, t.x).normalized;
            if (i > 0) along += (_line[i] - _line[i - 1]).magnitude;
            Vector4 col = new Vector4(glass.r, glass.g, glass.b, glass.a);
            Add(_line[i] + nor * half, new Vector4(along, 1f, 5f, 0f), col);
            Add(_line[i] - nor * half, new Vector4(along, -1f, 5f, 0f), col);
            if (i > 0)
            {
                int v = _verts.Count - 4;
                _tris.Add(v); _tris.Add(v + 1); _tris.Add(v + 3);
                _tris.Add(v); _tris.Add(v + 3); _tris.Add(v + 2);
            }
        }
        Upload(_walls);

        Clear();
        Vector2 mid = Vector2.zero;
        for (int i = 0; i < n; i++) mid += _line[i];
        mid /= n;
        Vector4 back = new Vector4(glassBack.r, glassBack.g, glassBack.b, glassBack.a);
        Add(mid, new Vector4(mid.x, mid.y, 7f, 0f), back);
        for (int i = 0; i < n; i++) Add(_line[i], new Vector4(_line[i].x, _line[i].y, 7f, 0f), back);
        for (int i = 0; i < n; i++)
        {
            _tris.Add(0); _tris.Add(1 + i); _tris.Add(1 + (i + 1) % n);
        }
        Upload(_panel);
    }

    void Clear()
    {
        _verts.Clear(); _uv0.Clear(); _uv1.Clear(); _uv2.Clear(); _tris.Clear();
    }

    void Add(Vector2 p, Vector4 uv0, Vector4 color)
    {
        _verts.Add(new Vector3(p.x, p.y, 0f));
        _uv0.Add(uv0);
        _uv1.Add(color);
        _uv2.Add(Vector4.zero);
    }

    void Upload(Mesh mesh)
    {
        mesh.SetVertices(_verts);
        mesh.SetUVs(0, _uv0);
        mesh.SetUVs(1, _uv1);
        mesh.SetUVs(2, _uv2);
        mesh.SetTriangles(_tris, 0, false);
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 10000f);
    }
}
