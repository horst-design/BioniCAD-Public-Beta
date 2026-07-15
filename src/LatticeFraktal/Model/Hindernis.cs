using System.Numerics;

namespace LatticeFraktal.Model;

/// <summary>
/// Sperrzone (Kugel oder Box), die der Baum umwächst. Spiegelt die Logik der
/// Browser-Vorschau: "drinnen?"-Test, Richtungs-Ablenkung beim Wachstum und
/// Projektion von Knoten auf die Oberfläche.
/// </summary>
public sealed class Hindernis
{
    public bool IstBox { get; init; }
    public Vector3 Zentrum { get; init; }
    public float Radius { get; init; }            // Kugel
    public Vector3 HalbGroesse { get; init; }     // Box (halbe Kantenlängen)

    public bool Innerhalb(Vector3 p, float extra)
    {
        if (IstBox)
            return MathF.Abs(p.X - Zentrum.X) < HalbGroesse.X + extra
                && MathF.Abs(p.Y - Zentrum.Y) < HalbGroesse.Y + extra
                && MathF.Abs(p.Z - Zentrum.Z) < HalbGroesse.Z + extra;
        float r = Radius + extra;
        return Vector3.DistanceSquared(p, Zentrum) < r * r;
    }

    /// <summary>Schiebt einen Punkt so nach außen, dass die Astoberfläche tangential aufliegt.</summary>
    public Vector3 SchiebeNachAussen(Vector3 pos, float gap, float knotenRadius)
    {
        if (IstBox)
        {
            float ex = gap + knotenRadius;
            float dx = pos.X - Zentrum.X, dy = pos.Y - Zentrum.Y, dz = pos.Z - Zentrum.Z;
            float ax = HalbGroesse.X + ex, ay = HalbGroesse.Y + ex, az = HalbGroesse.Z + ex;
            if (MathF.Abs(dx) >= ax || MathF.Abs(dy) >= ay || MathF.Abs(dz) >= az)
                return pos;
            float px = ax - MathF.Abs(dx), py = ay - MathF.Abs(dy), pz = az - MathF.Abs(dz);
            if (px <= py && px <= pz) return new Vector3(Zentrum.X + (dx < 0 ? -ax : ax), pos.Y, pos.Z);
            if (py <= pz)             return new Vector3(pos.X, Zentrum.Y + (dy < 0 ? -ay : ay), pos.Z);
            return new Vector3(pos.X, pos.Y, Zentrum.Z + (dz < 0 ? -az : az));
        }

        Vector3 d = pos - Zentrum;
        float dist = d.Length();
        float minD = Radius + gap + knotenRadius;
        if (dist >= minD) return pos;
        if (dist < 1e-6f) return Zentrum + new Vector3(0, 0, minD);
        return Zentrum + d * (minD / dist);
    }

    /// <summary>Projiziert alle Knoten eines Baums auf die Hindernis-Oberflächen (Wurzel bleibt fix).</summary>
    public static void ProjiziereBaum(Tree baum, IReadOnlyList<Hindernis> hindernisse, float gap)
    {
        if (hindernisse.Count == 0) return;
        foreach (Node n in baum.Knoten)
        {
            if (n.IstWurzel) continue;
            Vector3 p = n.Position;
            foreach (Hindernis h in hindernisse)
                p = h.SchiebeNachAussen(p, gap, n.Durchmesser * 0.5f);
            n.Position = p;
        }
    }
}
