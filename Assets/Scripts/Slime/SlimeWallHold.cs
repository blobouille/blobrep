using UnityEngine;

// Les parois du réservoir sont des limites, pas des tremplins : un slime qui s'écrase contre une paroi (lâché dans le coin)
// ne repart pas en roulant vers l'autre côté. Tant qu'il touche une paroi, la vitesse de son centre qui l'éloigne de cette
// paroi (face intérieure : BlobDropper.WallHalfWidth) est ramenée à 'maxAwaySpeed' ; la même correction est donnée à toutes ses
// capsules, donc sa déformation (écrasement, tremblement) n'est pas touchée. Dans LevelTick, après le pas des blobs : déterministe.
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(BlobDropper))]
public class SlimeWallHold : MonoBehaviour, ILevelPhysics, ILevelTick
{
    [Header("Parois")]
    [Tooltip("Hauteur du haut des parois, en m (au-dessus, rien n'est retenu).")]
    public float wallTop = 14f;
    [Tooltip("Une capsule à moins de cette distance de la paroi la touche, en m. Trop petit (0,03) : en s'écrasant à\n" +
             "l'atterrissage, le slime est repoussé hors de cette marge en un pas, n'est plus retenu et repart en roulant.")]
    [Min(0f)] public float contactMargin = 0.08f;
    [Tooltip("Vitesse max (km/h) à laquelle un slime qui touche une paroi peut s'en éloigner. 0 = il reste contre la paroi tant\n" +
             "qu'il la touche. Ne freine pas le mouvement vers la paroi ni vertical.")]
    [SpeedKmh] [Min(0f)] public float maxAwaySpeed = 0f;

    BlobDropper _dropper;
    BlobFusion _fusion;

    public int Holds { get; private set; }

    void OnEnable()
    {
        _dropper = GetComponent<BlobDropper>();
        _fusion = GetComponent<BlobFusion>();
        Holds = 0;
    }

    public void LevelTick()
    {
        var blobs = _dropper.Blobs;
        float axis = transform.position.x, dt = Time.fixedDeltaTime;
        for (int i = 0; i < blobs.Count; i++)
        {
            var blob = blobs[i];
            int n = blob.CapsuleCount;
            if (n == 0) continue;
            // Tri grossier : une capsule + 2 diamètres (écrasé à l'atterrissage, le slime dépasse son diamètre ; avec 1 seul
            // diamètre, la capsule 0, côté droit de l'anneau, écartait un slime écrasé contre la paroi GAUCHE).
            Vector2 p0 = blob.Capsule(0).position;
            if (Mathf.Abs(p0.x - axis) + 2f * blob.diameter < _dropper.WallHalfWidth - contactMargin) continue;
            if (_fusion != null && _fusion.IsMerging(blob)) continue;

            float rc = blob.CapsuleRadius, reach = _dropper.WallHalfWidth - contactMargin;
            int side = 0;
            for (int k = 0; k < n; k++)
            {
                var body = blob.Capsule(k);
                Vector2 p = body.position;
                if (p.y > wallTop) continue;
                // + chemin d'un pas : repoussé par la paroi pendant le dernier pas, il la touchait encore (quelle que soit la fréquence).
                float x = p.x - axis, slip = dt * Mathf.Abs(body.linearVelocity.x);
                if (x + rc + slip >= reach) { side = 1; break; }
                if (-x + rc + slip >= reach) { side = -1; break; }
            }
            if (side == 0) continue;

            float vx = 0f;
            for (int k = 0; k < n; k++) vx += blob.Capsule(k).linearVelocity.x;
            vx /= n;
            float away = -side * vx; // > 0 : s'éloigne de la paroi
            if (away <= maxAwaySpeed) continue;
            float dv = side * (away - maxAwaySpeed);
            for (int k = 0; k < n; k++)
            {
                var body = blob.Capsule(k);
                Vector2 v = body.linearVelocity;
                v.x += dv;
                body.linearVelocity = v;
            }
            Holds++;
        }
    }
}
