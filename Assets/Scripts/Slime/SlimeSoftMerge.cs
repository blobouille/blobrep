using System.Collections.Generic;
using UnityEngine;

// Fusions douces (Space Endless) : le slime né d'une fusion apparaît à sa taille finale sur la cacahuète des deux parents,
// à cheval sur des voisins écrasés ; Box2D les sépare d'un coup et tout saute (mesuré : né jusqu'à 6 m/s, voisins 9 m/s,
// plus vite qu'un tir à fond). Pendant 'calmTime' après chaque fusion, la vitesse du centre du slime né et de ses voisins
// directs est ramenée à 'calmSpeed' au plus (seul l'excès est retiré, même correction pour toutes leurs capsules :
// l'écrasement et la gélatine de la fusion restent). Dans LevelTick, après BlobFusion : déterministe. Scène Space seulement.
[DisallowMultipleComponent]
[RequireComponent(typeof(BlobDropper), typeof(BlobFusion))]
public class SlimeSoftMerge : MonoBehaviour, ILevelPhysics, ILevelTick
{
    [Tooltip("Durée (s) du calme après une fusion.")]
    [Min(0f)] public float calmTime = 0.8f;
    [Tooltip("Vitesse max (km/h) du slime né et de ses voisins pendant ce temps.")]
    [SpeedKmh] [Min(0f)] public float calmSpeed = 4f;
    [Tooltip("Voisin = slime dont le bord est à moins de cette distance (m) du bord du slime né.")]
    [Min(0f)] public float neighbourGap = 0.4f;

    struct Calm { public BLOB blob; public int until; }

    BlobDropper _dropper;
    BlobFusion _fusion;
    readonly List<Calm> _calm = new List<Calm>();

    void OnEnable()
    {
        _dropper = GetComponent<BlobDropper>();
        _fusion = GetComponent<BlobFusion>();
        _calm.Clear();
        _fusion.Merged += OnMerged;
    }

    void OnDisable()
    {
        _fusion.Merged -= OnMerged;
        _calm.Clear();
    }

    void OnMerged(BLOB merged, int level, Vector2 at) =>
        _calm.Add(new Calm { blob = merged, until = LevelSimulation.Step + Mathf.CeilToInt(calmTime / Time.fixedDeltaTime) });

    public void LevelTick()
    {
        if (_calm.Count == 0) return;
        int step = LevelSimulation.Step;
        var blobs = _dropper.Blobs;
        for (int c = _calm.Count - 1; c >= 0; c--)
        {
            var born = _calm[c].blob;
            if (step > _calm[c].until || !Contains(blobs, born)) { _calm.RemoveAt(c); continue; }
            Vector2 center = Center(born);
            Limit(born);
            for (int i = 0; i < blobs.Count; i++)
            {
                var other = blobs[i];
                if (other == born) continue;
                float reach = 0.5f * (born.diameter + other.diameter) + neighbourGap;
                if ((Center(other) - center).sqrMagnitude < reach * reach) Limit(other);
            }
        }
    }

    static bool Contains(IReadOnlyList<BLOB> blobs, BLOB blob)
    {
        for (int i = 0; i < blobs.Count; i++) if (blobs[i] == blob) return true;
        return false;
    }

    static Vector2 Center(BLOB blob)
    {
        Vector2 c = Vector2.zero;
        int n = blob.CapsuleCount;
        for (int k = 0; k < n; k++) c += blob.Capsule(k).position;
        return n > 0 ? c / n : (Vector2)blob.transform.position;
    }

    void Limit(BLOB blob)
    {
        int n = blob.CapsuleCount;
        if (n == 0) return;
        Vector2 v = Vector2.zero;
        for (int k = 0; k < n; k++) v += blob.Capsule(k).linearVelocity;
        v /= n;
        float speed = v.magnitude;
        if (speed <= calmSpeed) return;
        Vector2 dv = v * (calmSpeed / speed - 1f);
        for (int k = 0; k < n; k++)
        {
            var b = blob.Capsule(k);
            b.linearVelocity += dv;
        }
    }
}
