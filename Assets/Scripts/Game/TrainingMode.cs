using System.Collections.Generic;
using Unity.U2D.Physics;
using UnityEngine;
using UnityEngine.InputSystem;

// Mode entraînement : points de sauvegarde exacts pour s'entraîner sur un passage avant de faire le niveau d'une traite.
// Un point mémorise les entrées de chaque pas depuis le départ : y revenir = redémarrer le niveau puis rejouer
// ces entrées en accéléré. Tout le niveau (blob, flippers, obstacles, plus tard plateformes mobiles…) est donc
// exactement dans l'état du moment de la sauvegarde. Coût du retour ≈ proportionnel au temps écoulé depuis le départ.
// Dans l'éditeur, les points survivent aux sorties de Play (édition du niveau : on repart du point jaune). Si le niveau
// a été modifié avant un point, le rejeu n'y retombe plus : le blob est alors replacé comme le fantôme.
[ExecuteAlways]
[DisallowMultipleComponent]
public class TrainingMode : MonoBehaviour
{
    [Header("Touches")]
    [Tooltip("Crée un point de sauvegarde à l'instant présent (fantôme du blob affiché à cet endroit).")]
    public Key saveKey = Key.S;
    [Tooltip("Revient au dernier point pratiqué. Réappuyer dans le délai 'previousChainDelay' : point précédent, et ainsi de suite.")]
    public Key previousKey = Key.Tab;
    [Tooltip("Va au point de sauvegarde suivant, plus récent. Attention : ne pas utiliser une touche de flipper.")]
    public Key nextKey = Key.LeftShift;

    [Header("Points de sauvegarde")]
    [Tooltip("Nombre maximum de points gardés. Au-delà, le plus ancien est supprimé. R efface tout.")]
    [Min(1)] public int maxCheckpoints = 100;
    [Tooltip("Coché : sauvegarder après être revenu à un point efface tous les points qui le suivent (les points restent dans\n" +
             "l'ordre du niveau, comme une progression). Décoché : tous les points sont gardés, parcourus dans l'ordre de création.")]
    public bool discardNewerOnSave = true;
    [Tooltip("Délai, en secondes réelles, pour enchaîner les appuis sur 'previousKey' : un nouvel appui dans ce délai\n" +
             "recule d'un point au lieu de revenir au même.")]
    [Min(0f)] public float previousChainDelay = 0.5f;

    [Header("Édition du niveau (éditeur uniquement)")]
    [Tooltip("Lancer Play repart directement du point jaune (dernier point pratiqué) au lieu du départ du niveau.\n" +
             "Les points sont gardés entre deux Play et à la fermeture de Unity (dossier UserSettings/TrainingCheckpoints).\n" +
             "Choisir le point : clic sur un fantôme dans la Scene view, hors Play (re-clic sur le jaune = départ normal).")]
    public bool startAtCurrentOnPlay = true;
    [Tooltip("Écart de position, en unités Unity (absolu), entre le rejeu et le fantôme au-delà duquel le niveau est considéré\n" +
             "comme modifié avant ce point : le blob est alors replacé exactement comme le fantôme (position, vitesse).")]
    [Min(0f)] public float divergenceTolerance = 0.001f;

    [Header("Fantômes")]
    [Tooltip("Affiche le blob tel qu'il était à chaque point de sauvegarde (rendu debug de la physique, gizmos hors Play).")]
    public bool showGhosts = true;
    [Tooltip("Couleur des fantômes des points de sauvegarde.")]
    public Color ghostColor = new Color(1f, 1f, 1f, 0.25f);
    [Tooltip("Couleur du fantôme du dernier point pratiqué (celui où 'previousKey' ramène).")]
    public Color currentGhostColor = new Color(1f, 0.85f, 0.2f, 0.7f);

    class Checkpoint
    {
        public uint[] inputs;
        public List<Ghost> ghosts = new List<Ghost>();
    }

    // Un fantôme par blob, dans l'ordre de BLOB.Active.
    [System.Serializable]
    class Ghost
    {
        public Vector2 center1, center2;
        public float radius;
        public bool broken;
        public List<BLOB.CapsuleState> capsules = new List<BLOB.CapsuleState>();
    }

    readonly List<Checkpoint> _checkpoints = new List<Checkpoint>();
    readonly List<BLOB.CapsuleState> _states = new List<BLOB.CapsuleState>();
    int _cursor = -1;
    float _lastPreviousTime = float.NegativeInfinity;
    bool _restoring;
    bool _started;

    public int Count => _checkpoints.Count;
    public int Cursor => _cursor;

    void OnEnable()
    {
        LevelRestart.Restarted += OnRestarted;
        Load();
    }

    void OnDisable() => LevelRestart.Restarted -= OnRestarted;

    void Start()
    {
        if (!Application.isPlaying) return;
        // Le départ du niveau (au chargement de la scène) a eu lieu avant : il n'efface pas les points.
        _started = true;
        if (Application.isEditor && startAtCurrentOnPlay && _cursor >= 0)
            LevelSimulation.RunBeforeNextStep(() => { if (_cursor >= 0 && _cursor < _checkpoints.Count) Restore(_cursor); });
    }

    void OnRestarted()
    {
        if (_restoring || !_started) return;
        _checkpoints.Clear();
        _cursor = -1;
        Persist();
    }

    void Update()
    {
        if (!Application.isPlaying) return;
        var keyboard = Keyboard.current;
        if (keyboard == null) return;
        if (keyboard[saveKey].wasPressedThisFrame) LevelSimulation.RunBeforeNextStep(Save);
        if (keyboard[previousKey].wasPressedThisFrame)
        {
            bool chained = Time.realtimeSinceStartup - _lastPreviousTime <= previousChainDelay;
            LevelSimulation.RunBeforeNextStep(() => Previous(chained));
        }
        if (keyboard[nextKey].wasPressedThisFrame) LevelSimulation.RunBeforeNextStep(Next);
    }

    void Save()
    {
        var cp = new Checkpoint { inputs = new uint[LevelSimulation.Step] };
        var log = LevelInput.Log;
        for (int i = 0; i < cp.inputs.Length; i++) cp.inputs[i] = log[i];

        foreach (var blob in BLOB.Active)
        {
            blob.GetCapsuleStates(_states);
            var shape = blob.CapsuleShape;
            cp.ghosts.Add(new Ghost
            {
                center1 = shape.center1,
                center2 = shape.center2,
                radius = shape.radius,
                broken = blob.IsBroken,
                capsules = new List<BLOB.CapsuleState>(_states)
            });
        }

        if (discardNewerOnSave && _cursor >= 0 && _cursor < _checkpoints.Count - 1)
            _checkpoints.RemoveRange(_cursor + 1, _checkpoints.Count - _cursor - 1);

        _checkpoints.Add(cp);
        if (_checkpoints.Count > maxCheckpoints) _checkpoints.RemoveAt(0);
        _cursor = _checkpoints.Count - 1;
        Persist();
    }

    void Previous(bool chained)
    {
        if (_checkpoints.Count == 0) return;
        if (_cursor < 0) _cursor = _checkpoints.Count - 1;
        else if (chained) _cursor = Mathf.Max(0, _cursor - 1);
        Restore(_cursor);
        // Délai compté depuis la fin du retour : un long rejeu ne doit pas faire rater l'enchaînement.
        _lastPreviousTime = Time.realtimeSinceStartup;
    }

    void Next()
    {
        if (_checkpoints.Count == 0) return;
        _cursor = _cursor < 0 ? _checkpoints.Count - 1 : Mathf.Min(_checkpoints.Count - 1, _cursor + 1);
        Restore(_cursor);
    }

    // Va au point 'index' (ordre de la liste) et en fait le point pratiqué : 'previousKey' y ramènera ensuite.
    // Pour une future sélection directe (clic / toucher sur un fantôme), via LevelSimulation.RunBeforeNextStep.
    public void GoTo(int index)
    {
        if (index < 0 || index >= _checkpoints.Count) return;
        _cursor = index;
        Restore(index);
    }

    // Hors Play (Scene view) : choisit le point de départ du prochain Play. -1 = départ normal du niveau.
    public void Select(int index)
    {
        _cursor = index >= 0 && index < _checkpoints.Count ? index : -1;
        Persist();
    }

    void Restore(int index)
    {
        var cp = _checkpoints[index];
        _restoring = true;
        try
        {
            LevelRestart.Restart();
        }
        finally
        {
            _restoring = false;
        }
        LevelSimulation.FastForward(cp.inputs, cp.inputs.Length, () => MatchGhosts(cp, index));
        Persist();
    }

    // Rejeu exact si rien n'a changé avant le point. Sinon (niveau modifié) : blob replacé comme le fantôme.
    // Le reste du niveau garde l'état du rejeu. Toujours déterministe : même niveau + même point = même résultat.
    void MatchGhosts(Checkpoint cp, int index)
    {
        var blobs = BLOB.Active;
        for (int k = 0; k < cp.ghosts.Count && k < blobs.Count; k++)
        {
            var ghost = cp.ghosts[k];
            blobs[k].GetCapsuleStates(_states);
            if (!Diverged(ghost, blobs[k])) continue;
            if (_states.Count != ghost.capsules.Count)
            {
                Debug.LogWarning($"TrainingMode : point {index + 1}, le blob n'a plus le même nombre de capsules, impossible de le replacer.");
                continue;
            }
            blobs[k].RestoreCapsules(ghost.capsules, ghost.broken);
            Debug.Log($"TrainingMode : point {index + 1}, niveau modifié avant ce point : blob replacé comme le fantôme.");
        }
    }

    bool Diverged(Ghost ghost, BLOB blob)
    {
        if (blob.IsBroken != ghost.broken || _states.Count != ghost.capsules.Count) return true;
        float tolerance = divergenceTolerance * divergenceTolerance;
        for (int i = 0; i < _states.Count; i++)
            if ((_states[i].position - ghost.capsules[i].position).sqrMagnitude > tolerance) return true;
        return false;
    }

    // Fantôme touché au point 'world' (disque englobant le blob), le plus proche de son centre.
    public bool GhostAt(Vector2 world, out int index)
    {
        index = -1;
        float best = float.PositiveInfinity;
        for (int c = 0; c < _checkpoints.Count; c++)
            foreach (var g in _checkpoints[c].ghosts)
            {
                if (g.capsules.Count == 0) continue;
                Vector2 center = Vector2.zero;
                foreach (var s in g.capsules) center += s.position;
                center /= g.capsules.Count;
                float radius = 0f;
                foreach (var s in g.capsules) radius = Mathf.Max(radius, (s.position - center).magnitude);
                radius += g.radius + 0.5f * (g.center2 - g.center1).magnitude;
                float d = (world - center).sqrMagnitude;
                if (d <= radius * radius && d < best) { best = d; index = c; }
            }
        return index >= 0;
    }

    void LateUpdate()
    {
        if (!Application.isPlaying || !showGhosts) return;
        var world = PhysicsWorld.defaultWorld;
        for (int c = 0; c < _checkpoints.Count; c++)
        {
            var color = c == _cursor ? currentGhostColor : ghostColor;
            foreach (var g in _checkpoints[c].ghosts)
            {
                var capsule = new CapsuleGeometry { center1 = g.center1, center2 = g.center2, radius = g.radius };
                foreach (var s in g.capsules)
                    world.DrawGeometry(capsule, new PhysicsTransform(s.position, PhysicsRotate.FromRadians(s.angle)), color, 0f,
                        PhysicsWorld.DrawFillOptions.Outline);
            }
        }
    }

    void OnDrawGizmos()
    {
        if (Application.isPlaying || !showGhosts) return;
        for (int c = 0; c < _checkpoints.Count; c++)
        {
            Gizmos.color = c == _cursor ? currentGhostColor : ghostColor;
            foreach (var g in _checkpoints[c].ghosts)
                foreach (var s in g.capsules)
                {
                    Gizmos.matrix = Matrix4x4.TRS(s.position, Quaternion.Euler(0f, 0f, s.angle * Mathf.Rad2Deg), Vector3.one);
                    DrawCapsule(g.center1, g.center2, g.radius);
                }
        }
        Gizmos.matrix = Matrix4x4.identity;
    }

    static void DrawCapsule(Vector2 a, Vector2 b, float r)
    {
        Vector2 axis = (b - a).sqrMagnitude > 1e-12f ? (b - a).normalized : Vector2.up;
        Vector2 n = new Vector2(-axis.y, axis.x) * r;
        Gizmos.DrawLine(a + n, b + n);
        Gizmos.DrawLine(a - n, b - n);
        const int N = 12;
        for (int i = 0; i < N; i++)
        {
            float a0 = i * 2f * Mathf.PI / N, a1 = (i + 1) * 2f * Mathf.PI / N;
            var d0 = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * r;
            var d1 = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * r;
            Gizmos.DrawLine(a + d0, a + d1);
            Gizmos.DrawLine(b + d0, b + d1);
        }
    }

    // Fichier : entrées compressées (valeur, nombre de pas répétés), elles changent rarement d'un pas à l'autre.
    [System.Serializable]
    class StoredCheckpoint
    {
        public int[] inputValues;
        public int[] inputRuns;
        public List<Ghost> ghosts;
    }

    [System.Serializable]
    class StoredFile
    {
        public int cursor = -1;
        public List<StoredCheckpoint> checkpoints = new List<StoredCheckpoint>();
    }

    string FilePath
    {
        get
        {
            string scene = gameObject.scene.path;
            if (string.IsNullOrEmpty(scene)) return null;
            string name = scene.Replace('/', '_').Replace('\\', '_').Replace(':', '_');
            return System.IO.Path.Combine(Application.dataPath, "..", "UserSettings", "TrainingCheckpoints", name + ".json");
        }
    }

    void Persist()
    {
        if (!Application.isEditor) return;
        string path = FilePath;
        if (path == null) return;

        var file = new StoredFile { cursor = _cursor };
        var values = new List<int>();
        var runs = new List<int>();
        foreach (var cp in _checkpoints)
        {
            values.Clear();
            runs.Clear();
            for (int i = 0; i < cp.inputs.Length; i++)
            {
                int v = unchecked((int)cp.inputs[i]);
                if (values.Count > 0 && values[values.Count - 1] == v) runs[runs.Count - 1]++;
                else { values.Add(v); runs.Add(1); }
            }
            file.checkpoints.Add(new StoredCheckpoint { inputValues = values.ToArray(), inputRuns = runs.ToArray(), ghosts = cp.ghosts });
        }

        try
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            System.IO.File.WriteAllText(path, JsonUtility.ToJson(file));
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"TrainingMode : points de sauvegarde non enregistrés ({e.Message}).");
        }
    }

    void Load()
    {
        _checkpoints.Clear();
        _cursor = -1;
        if (!Application.isEditor) return;
        string path = FilePath;
        if (path == null || !System.IO.File.Exists(path)) return;

        StoredFile file;
        try
        {
            file = JsonUtility.FromJson<StoredFile>(System.IO.File.ReadAllText(path));
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"TrainingMode : fichier de points illisible ({e.Message}).");
            return;
        }
        if (file?.checkpoints == null) return;

        foreach (var sc in file.checkpoints)
        {
            if (sc.inputValues == null || sc.inputRuns == null || sc.inputValues.Length != sc.inputRuns.Length) continue;
            int total = 0;
            foreach (int r in sc.inputRuns) total += r;
            var inputs = new uint[total];
            int k = 0;
            for (int i = 0; i < sc.inputValues.Length; i++)
                for (int r = 0; r < sc.inputRuns[i]; r++) inputs[k++] = unchecked((uint)sc.inputValues[i]);
            _checkpoints.Add(new Checkpoint { inputs = inputs, ghosts = sc.ghosts ?? new List<Ghost>() });
        }
        _cursor = file.cursor >= 0 && file.cursor < _checkpoints.Count ? file.cursor : -1;
    }
}
