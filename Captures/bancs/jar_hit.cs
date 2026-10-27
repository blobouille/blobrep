// Play : une balle lâchée au-dessus de l'épaule du bocal et une à l'intérieur ; positions au bout de 3 s (doivent rester posées).
var j = UnityEngine.Object.FindAnyObjectByType<JarShape2D>();
Vector2 o = j.transform.position;
var w = Unity.U2D.Physics.PhysicsWorld.defaultWorld;
Unity.U2D.Physics.PhysicsBody Ball(Vector2 p) { var d = Unity.U2D.Physics.PhysicsBodyDefinition.defaultDefinition; d.type = Unity.U2D.Physics.PhysicsBody.BodyType.Dynamic; d.position = p; var b = w.CreateBody(d); b.CreateShape(new Unity.U2D.Physics.CircleGeometry { radius = 0.15f }, Unity.U2D.Physics.PhysicsShapeDefinition.defaultDefinition); return b; }
var outside = Ball(o + new Vector2(j.radius * 0.9f, j.InnerTop + 1f));
var inside = Ball(o + new Vector2(0f, 0.5f));
for (int i = 0; i < 180; i++) w.Simulate(1f / 60f);
var s = $"bocal y0={o.y:0.00} haut={o.y + j.InnerTop:0.00} | dehors {outside.position} | dedans {inside.position}";
outside.Destroy(); inside.Destroy();
return s;
