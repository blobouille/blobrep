using System;
using System.Collections.Generic;
using UnityEngine;

// Campagne (mode Aventure) : zones de niveaux, chacun réglé à la main (graine, coups, difficulté…). Lue par la carte (GumoriMap)
// et par le réservoir (GumoriLevelSetup, qui applique le niveau choisi à GumoriDropper et GumoriGoal). Numéro de niveau global :
// 0 = premier niveau de la première zone, les zones suivantes continuent la numérotation.
[CreateAssetMenu(menuName = "Gumori/Campagne", fileName = "Campagne")]
public class GumoriCampaign : ScriptableObject
{
    [Header("Étoiles")]
    [Tooltip("Niveau réussi = 1 étoile. 2 étoiles s'il reste au moins cette part des coups quand l'objectif est atteint\n" +
             "(0,1 = 10 % des coups du niveau). À caler sur de vraies parties.")]
    [Range(0f, 1f)] public float twoStarsMovesLeft = 0.1f;
    [Tooltip("3 étoiles s'il reste au moins cette part des coups quand l'objectif est atteint (0,2 = 20 %).")]
    [Range(0f, 1f)] public float threeStarsMovesLeft = 0.2f;

    public List<GumoriZone> zones = new List<GumoriZone> { GumoriZone.DefaultPlain() };

    public int LevelCount
    {
        get
        {
            int n = 0;
            for (int i = 0; i < zones.Count; i++) n += zones[i].levels.Count;
            return n;
        }
    }

    public GumoriLevel Level(int index)
    {
        int zone = ZoneOf(index, out int first);
        return zone < 0 ? null : zones[zone].levels[index - first];
    }

    // Zone du niveau 'index' (-1 si hors campagne) et numéro global de son premier niveau.
    public int ZoneOf(int index, out int first)
    {
        first = 0;
        if (index < 0) return -1;
        for (int z = 0; z < zones.Count; z++)
        {
            int count = zones[z].levels.Count;
            if (index < first + count) return z;
            first += count;
        }
        return -1;
    }

    public int Stars(GumoriLevel level, int movesLeft)
    {
        float left = (float)movesLeft / Mathf.Max(1, level.maxDrops);
        return left >= threeStarsMovesLeft ? 3 : left >= twoStarsMovesLeft ? 2 : 1;
    }
}

[Serializable]
public class GumoriZone
{
    public string name = "Plaine";
    public List<GumoriLevel> levels = new List<GumoriLevel>();

    // Zone 1 : 30 niveaux, graines toutes différentes, 30 coups. 1 à 5 faciles, puis moyens avec une respiration facile après
    // chaque niveau difficile (10, 20, 30).
    public static GumoriZone DefaultPlain()
    {
        var zone = new GumoriZone { name = "Plaine" };
        for (int n = 1; n <= 30; n++)
        {
            var d = n <= 5 || n % 10 == 1 ? GumoriGoal.Difficulty.Easy : n % 10 == 0 ? GumoriGoal.Difficulty.Hard : GumoriGoal.Difficulty.Medium;
            zone.levels.Add(new GumoriLevel { seed = 100 + n, difficulty = d });
        }
        return zone;
    }
}

[Serializable]
public class GumoriLevel
{
    [Tooltip("Graine de la suite des gumori à lâcher : même graine = même suite, à chaque partie.")]
    public int seed = 1;
    [Tooltip("Nombre de coups (lâchers) du niveau.")]
    [Min(1)] public int maxDrops = 30;
    [Tooltip("Coché : objectif calculé selon la difficulté (même calcul que GumoriGoal : points sans chaîne en fusionnant\n" +
             "parfaitement la suite, divisés par 1 + marge). Décoché : 'targetScore' tel quel.")]
    public bool autoTarget = true;
    public GumoriGoal.Difficulty difficulty = GumoriGoal.Difficulty.Medium;
    [Tooltip("Objectif saisi à la main (utilisé seulement si 'autoTarget' est décoché).")]
    [Min(1)] public int targetScore = 3000;
    [Tooltip("Part de l'objectif à aller chercher avec des chaînes (0 = chaînes en bonus).")]
    [Range(0f, 1f)] public float comboShare;
    [Tooltip("Plus gros niveau qu'on peut recevoir à lâcher (0 = toujours le premier gumori, 3 = du 1er au 4e).")]
    [Min(0)] public int maxDropLevel = 3;
    [Tooltip("Préférence pour les petits gumori à lâcher (0 = tous aussi fréquents).")]
    [Range(0f, 1f)] public float smallBias;
    [Tooltip("Notes libres (mécaniques prévues pour ce niveau…). Sans effet sur le jeu.")]
    [TextArea(1, 3)] public string note = "";
}
