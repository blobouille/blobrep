using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Construction du maillage du sol de la carte (shader Slime/Map). Chaque sommet : position, normale, couleur, et deux jeux de
// paramètres pris dans 'a' (uv0 : balancement, sorte, paramètres) et 'b' (uv1 : pivot xyz + paramètre) au moment où il est
// ajouté. Formes : disque (ellipse) et tranche de cylindre.
public class GumoriMapMesh
{
    // Sortes (uv0.y) lues par le shader.
    public const float Ground = 0f, Path = 1f, Node = 3f, Water = 5f, Decal = 6f;

    readonly List<Vector3> _pos = new List<Vector3>(), _nor = new List<Vector3>();
    readonly List<Color> _col = new List<Color>();
    readonly List<Vector4> _uv0 = new List<Vector4>(), _uv1 = new List<Vector4>();
    readonly List<int> _tri = new List<int>();

    public Vector4 a, b;
    public int Count => _pos.Count;

    public void Clear()
    {
        _pos.Clear(); _nor.Clear(); _col.Clear(); _uv0.Clear(); _uv1.Clear(); _tri.Clear();
    }

    // Réglages courants : sorte, pivot, balancement, paramètres libres.
    public void Set(float kind, Vector3 pivot, float sway = 0f, float p0 = 0f, float p1 = 0f, float p2 = 0f)
    {
        a = new Vector4(sway, kind, p0, p1);
        b = new Vector4(pivot.x, pivot.y, pivot.z, p2);
    }

    // Couleur donnée en sRGB (comme dans l'inspecteur) : convertie en linéaire, les couleurs de sommets ne le sont pas d'office.
    public int V(Vector3 p, Vector3 n, Color c)
    {
        _pos.Add(p); _nor.Add(n); _col.Add(c.linear); _uv0.Add(a); _uv1.Add(b);
        return _pos.Count - 1;
    }

    public void T(int i, int j, int k) { _tri.Add(i); _tri.Add(j); _tri.Add(k); }

    public void Q(int i, int j, int k, int l) { T(i, j, k); T(i, k, l); }

    public Mesh ToMesh(Mesh mesh, string name)
    {
        if (mesh == null) mesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };
        mesh.Clear();
        mesh.name = name;
        mesh.indexFormat = _pos.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
        mesh.SetVertices(_pos);
        mesh.SetNormals(_nor);
        mesh.SetColors(_col);
        mesh.SetUVs(0, _uv0);
        mesh.SetUVs(1, _uv1);
        mesh.SetTriangles(_tri, 0, false);
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
        mesh.UploadMeshData(false);
        return mesh;
    }

    // Tranche verticale d'un cylindre de 'from' à 'to' (sans chapeau) ; le bas un peu plus sombre.
    public void Cylinder(Vector3 from, Vector3 to, float ra, float rb, Color col, int seg = 8)
    {
        int start = _pos.Count;
        for (int j = 0; j <= seg; j++)
        {
            float lon = 2f * Mathf.PI * j / seg;
            var d = new Vector3(Mathf.Cos(lon), 0f, Mathf.Sin(lon));
            V(from + d * ra, d, col * 0.85f);
            V(to + d * rb, d, col);
        }
        for (int j = 0; j < seg; j++)
        {
            int k = start + 2 * j;
            Q(k, k + 1, k + 3, k + 2);
        }
    }

    // Disque horizontal (ellipse rx × rz) à la hauteur de c.
    public void Disc(Vector3 c, float rx, float rz, Color col, int seg = 16)
    {
        int center = V(c, Vector3.up, col);
        int ring = _pos.Count;
        for (int j = 0; j <= seg; j++)
        {
            float lon = 2f * Mathf.PI * j / seg;
            V(c + new Vector3(Mathf.Cos(lon) * rx, 0f, Mathf.Sin(lon) * rz), Vector3.up, col);
        }
        for (int j = 0; j < seg; j++) T(center, ring + j + 1, ring + j);
    }
}
