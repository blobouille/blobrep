var sb = new System.Text.StringBuilder();
foreach (var t in new[]{ typeof(Unity.U2D.Physics.PhysicsHingeJointDefinition), typeof(Unity.U2D.Physics.PhysicsRotate), typeof(Unity.U2D.Physics.CircleGeometry), typeof(Unity.U2D.Physics.PhysicsBody) }) {
  sb.Append(t.Name + ": ");
  foreach (var m in t.GetMembers(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.DeclaredOnly))
    if (m.MemberType == System.Reflection.MemberTypes.Field || m.MemberType == System.Reflection.MemberTypes.Property) sb.Append(m.Name + " ");
  sb.AppendLine();
}
return sb.ToString();
