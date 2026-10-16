using System.Collections.Generic;
using Unity.U2D.Physics;
using UnityEngine;
using UnityEngine.InputSystem;

// Mode entraînement : points de sauvegarde exacts pour s'entraîner sur un passage avant de faire le niveau d'une traite.
// Un point mémorise les entrées de chaque pas depuis le départ : y revenir = redémarrer le niveau puis rejouer
// ces entrées en accéléré. Tout le niveau (blob, flippers, obstacles, plus tard plateformes mobiles…) est donc
// exactement dans l'état du moment de la sauvegarde. Coût du retour ≈ proportionnel au temps écoulé depuis le départ.
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

    [Header("Fantômes")]
    [Tooltip("Affiche le blob tel qu'il était à chaque point de sauvegarde (rendu debug de la physique).")]
    public bool showGhosts = true;
    [Tooltip("Couleur des fantômes des points de sauvegarde.")]
    public Color ghostColor = new Color(1f, 1f, 1f, 0.25f);
    [Tooltip("Couleur du fantôme du dernier point pratiqué (celui où 'previousKey' ramène).")]
    public Color currentGhostColor = new Color(1f, 0.85f, 0.2f, 0.7f);

    class Checkpoint
    {
        public uint[] inputs;
        public readonly List<Ghost> ghosts = new List<Ghost>();
    }

    struct Ghost
    {
        public CapsuleGeometry capsule;
        public PhysicsTransform[] poses;
    }

    readonly List<Checkpoint> _checkpoints = new List<Checkpoint>();
    readonly List<PhysicsTransform> _poses = new List<PhysicsTransform>();
    int _cursor = -1;
    float _lastPreviousTime = float.NegativeInfinity;
    bool _restoring;

    public int Count => _checkpoints.Count;
    public int Cursor => _cursor;

    void OnEnable() => LevelRestart.Restarted += OnRestarted;
    void OnDisable() => LevelRestart.Restarted -= OnRestarted;

    void OnRestarted()
    {
        if (_restoring) return;
        _checkpoints.Clear();
        _cursor = -1;
    }

    void Update()
    {
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

        foreach (var blob in FindObjectsByType<BLOB>())
        {
            blob.GetCapsulePoses(_poses);
            if (_poses.Count > 0) cp.ghosts.Add(new Ghost { capsule = blob.CapsuleShape, poses = _poses.ToArray() });
        }

        if (discardNewerOnSave && _cursor >= 0 && _cursor < _checkpoints.Count - 1)
            _checkpoints.RemoveRange(_cursor + 1, _checkpoints.Count - _cursor - 1);

        _checkpoints.Add(cp);
        if (_checkpoints.Count > maxCheckpoints) _checkpoints.RemoveAt(0);
        _cursor = _checkpoints.Count - 1;
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
        LevelSimulation.FastForward(cp.inputs, cp.inputs.Length);
    }

    void LateUpdate()
    {
        if (!showGhosts) return;
        var world = PhysicsWorld.defaultWorld;
        for (int c = 0; c < _checkpoints.Count; c++)
        {
            var color = c == _cursor ? currentGhostColor : ghostColor;
            foreach (var g in _checkpoints[c].ghosts)
                foreach (var pose in g.poses)
                    world.DrawGeometry(g.capsule, pose, color, 0f, PhysicsWorld.DrawFillOptions.Outline);
        }
    }
}
