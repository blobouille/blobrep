using System.Collections.Generic;
using UnityEngine;

// Fusions douces (Space Endless) : le gumori né d'une fusion apparaît à sa taille finale sur la cacahuète des deux parents,
// à cheval sur des voisins écrasés ; Box2D les sépare d'un coup et tout saute (mesuré : né jusqu'à 6 m/s, voisins 9 m/s,
// plus vite qu'un tir à fond). Pendant 'calmTime' après chaque fusion, la vitesse du centre du gumori né et de ses voisins
// directs est ramenée à 'calmSpeed' au plus (seul l'excès est retiré, même correction pour toutes leurs capsules :
// l'écrasement et la gélatine de la fusion restent). Dans LevelTick, après GumoriFusion : déterministe. Scène Space seulement.
[DisallowMultipleComponent]
[RequireComponent(typeof(GumoriDropper), typeof(GumoriFusion))]
public class GumoriSoftMerge : MonoBehaviour, ILevelPhysics, ILevelTick
{
    [Tooltip("Durée (s) du calme après une fusion.")]
    [Min(0f)] public float calmTime = 0.8f;
    [Tooltip("Vitesse max (km/h) du gumori né et de ses voisins pendant ce temps.")]
    [SpeedKmh] [Min(0f)] public float calmSpeed = 4f;
    [Tooltip("Voisin = gumori dont le bord est à moins de cette distance (m) du bord du gumori né.")]
    [Min(0f)] public float neighbourGap = 0.4f;

    struct Calm { public Gumori blob; public int until; }

    GumoriDropper _dropper;
    GumoriFusion _fusion;
    readonly List<Calm> _calm = new List<Calm>();

    void OnEnable()
    {
        _dropper = GetComponent<GumoriDropper>();
        _fusion = GetComponent<GumoriFusion>();
        _calm.Clear();
        _fusion.Merged += OnMerged;
    }

    void OnDisable()
    {
        _fusion.Merged -= OnMerged;
        _calm.Clear();
    }

    void OnMerged(Gumori merged, int level, Vector2 at) =>
        _calm.Add(new Calm { blob = merged, until = LevelSimulation.Step + Mathf.CeilToInt(calmTime / Time.fixedDeltaTime) });

    public void LevelTick()
    {
        if (_calm.Count == 0) return;
        int step = LevelSimulation.Step;
        var blobs = _dropper.InPlay;
        for (int c = _calm.Count - 1; c >= 0; c--)
        {
            var born = _calm[c].blob;
            if (step > _calm[c].until || !GumoriDropper.IsInPlay(born)) { _calm.RemoveAt(c); continue; }
            Vector2 center = born.Center;
            Limit(born);
            for (int i = 0; i < blobs.Count; i++)
            {
                var other = blobs[i];
                if (other == born) continue;
                float reach = 0.5f * (born.diameter + other.diameter) + neighbourGap;
                if ((other.Center - center).sqrMagnitude < reach * reach) Limit(other);
            }
        }
    }

    void Limit(Gumori blob)
    {
        Vector2 v = blob.Velocity;
        float speed = v.magnitude;
        if (speed <= calmSpeed) return;
        blob.AddVelocity(v * (calmSpeed / speed - 1f));
    }
}
