using System.Collections.Generic;
using UnityEngine;

public enum PathShape { Circle, Triangle, Square, Polygon }

// Formes toutes faites pour une courbe (CurvePath2D) : trajectoires, et plus tard tout ce qui en a besoin.
// Code de jeu : réutilisable par le futur éditeur de niveaux en jeu.
public static class PathShapes
{
    // Longueur des tangentes d'un quart de cercle en Bézier cubique (rapport au rayon).
    const float Kappa = 0.5522847f;

    // Forme fermée centrée sur l'origine, parcourue dans le sens anti-horaire. 'radius' = rayon du cercle circonscrit
    // (unités Unity), 'rotation' en degrés, 'sides' pour Polygon (3 à 64).
    public static List<CurvePoint> Build(PathShape shape, float radius, float rotation, int sides)
    {
        var pts = new List<CurvePoint>();
        if (shape == PathShape.Circle)
        {
            for (int k = 0; k < 4; k++)
            {
                float a = (rotation + 90f * k) * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                var tangent = new Vector2(-dir.y, dir.x) * (Kappa * radius);
                pts.Add(new CurvePoint(dir * radius, HandleMode.Aligned) { handleIn = -tangent, handleOut = tangent });
            }
            return pts;
        }

        int n = shape == PathShape.Triangle ? 3 : shape == PathShape.Square ? 4 : Mathf.Clamp(sides, 3, 64);
        float start = n == 4 ? 45f : 90f;
        for (int k = 0; k < n; k++)
        {
            float a = (rotation + start + 360f * k / n) * Mathf.Deg2Rad;
            pts.Add(new CurvePoint(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius, HandleMode.Vector));
        }
        return pts;
    }

    public static void Apply(CurvePath2D path, PathShape shape, float radius, float rotation, int sides)
    {
        path.points = Build(shape, radius, rotation, sides);
        path.closed = true;
        path.RecomputeHandles();
        path.Rebuild();
    }

    // Agrandit / réduit toute la courbe (points et tangentes) autour de l'origine locale.
    public static void Scale(CurvePath2D path, float factor)
    {
        for (int i = 0; i < path.points.Count; i++)
        {
            var p = path.points[i];
            p.position *= factor; p.handleIn *= factor; p.handleOut *= factor;
            path.points[i] = p;
        }
        path.Rebuild();
    }

    // Tourne toute la courbe autour de l'origine locale, en degrés (positif = anti-horaire).
    public static void Rotate(CurvePath2D path, float degrees)
    {
        var q = Quaternion.Euler(0f, 0f, degrees);
        for (int i = 0; i < path.points.Count; i++)
        {
            var p = path.points[i];
            p.position = q * p.position; p.handleIn = q * p.handleIn; p.handleOut = q * p.handleOut;
            path.points[i] = p;
        }
        path.Rebuild();
    }
}
