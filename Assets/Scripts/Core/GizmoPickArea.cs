#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

// Zone de sélection pleine et invisible pour la Scene view : Unity sélectionne un objet en cliquant sur ses gizmos,
// et un gizmo en lignes ne se clique que pile sur le trait. Ce maillage invisible rend toute la surface cliquable.
// Éditeur uniquement : rien n'est affiché, rien n'existe en jeu.
public sealed class GizmoPickArea
{
    static readonly Color Invisible = new Color(0f, 0f, 0f, 0f);

    readonly List<Vector3> _vertices = new List<Vector3>();
    readonly List<int> _triangles = new List<int>();
    Mesh _mesh;
    int _hash;

    public void Clear()
    {
        _vertices.Clear();
        _triangles.Clear();
    }

    public void AddConvex(IReadOnlyList<Vector2> polygon)
    {
        int start = _vertices.Count;
        foreach (var p in polygon) _vertices.Add(p);
        for (int i = 1; i < polygon.Count - 1; i++) AddTriangle(start, start + i, start + i + 1);
    }

    public void AddCircle(Vector2 center, float radius, int segments = 24)
    {
        int start = _vertices.Count;
        _vertices.Add(center);
        for (int i = 0; i < segments; i++)
        {
            float a = i * 2f * Mathf.PI / segments;
            _vertices.Add(center + radius * new Vector2(Mathf.Cos(a), Mathf.Sin(a)));
        }
        for (int i = 0; i < segments; i++) AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % segments);
    }

    // Bande entre deux bords de même longueur (ex. bords gauche / droit d'une courbe épaisse).
    public void AddStrip(IReadOnlyList<Vector2> a, IReadOnlyList<Vector2> b, bool closed)
    {
        int n = Mathf.Min(a.Count, b.Count);
        if (n < 2) return;
        int start = _vertices.Count;
        for (int i = 0; i < n; i++) { _vertices.Add(a[i]); _vertices.Add(b[i]); }
        int segs = closed ? n : n - 1;
        for (int i = 0; i < segs; i++)
        {
            int i0 = start + 2 * i, i1 = start + 2 * ((i + 1) % n);
            AddTriangle(i0, i0 + 1, i1);
            AddTriangle(i1, i0 + 1, i1 + 1);
        }
    }

    // Les deux faces : cliquable quel que soit le sens des triangles.
    void AddTriangle(int a, int b, int c)
    {
        _triangles.Add(a); _triangles.Add(b); _triangles.Add(c);
        _triangles.Add(a); _triangles.Add(c); _triangles.Add(b);
    }

    public void Draw(Matrix4x4 localToWorld)
    {
        if (_triangles.Count == 0) return;
        Upload();
        var previousColor = Gizmos.color;
        var previousMatrix = Gizmos.matrix;
        Gizmos.color = Invisible;
        Gizmos.matrix = localToWorld;
        Gizmos.DrawMesh(_mesh);
        Gizmos.color = previousColor;
        Gizmos.matrix = previousMatrix;
    }

    // Le maillage n'est renvoyé au GPU que si la géométrie a changé.
    void Upload()
    {
        int hash = 17;
        unchecked
        {
            foreach (var v in _vertices) hash = hash * 31 + v.GetHashCode();
            hash = hash * 31 + _triangles.Count;
        }
        if (_mesh != null && hash == _hash) return;
        _hash = hash;

        if (_mesh == null)
            _mesh = new Mesh { hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild };
        _mesh.Clear();
        _mesh.SetVertices(_vertices);
        _mesh.SetTriangles(_triangles, 0);
        _mesh.RecalculateNormals();
        _mesh.RecalculateBounds();
    }

    public void Dispose()
    {
        if (_mesh != null) Object.DestroyImmediate(_mesh);
        _mesh = null;
    }
}
#endif
