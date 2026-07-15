using System.Numerics;
using LatticeFraktal.Model;

namespace LatticeFraktal.Engines;

/// <summary>
/// Dendritisches Wachstum nach dem Space-Colonization-Algorithmus
/// (Runions et al.). Unterstützt Hindernis-Ausweichung sowie einen zweiten,
/// verschränkt wachsenden Baum (Doppelbaum), der dem ersten ausweicht.
/// </summary>
public sealed class SpaceColonization : IWachstumsEngine
{
    private const float Epsilon = 1e-12f;
    private const int KnotenObergrenze = 200_000;

    public SkelettErgebnis Erzeuge(Vector3 start, IReadOnlyList<Target> ziele, EngineOptionen o)
    {
        var attraktoren = SammleAttraktoren(ziele);
        int gesamt = attraktoren.Count;
        var baum = new Tree(new Node(0, start));
        if (gesamt == 0)
            return new SkelettErgebnis { Baum = baum, ZieleGesamt = 0, ZieleErreicht = 0, Iterationen = 0 };

        var rng = new Random(o.Seed);
        float avoidSq = o.VermeideAbstand * o.VermeideAbstand;

        int iter = 0;
        for (; iter < o.MaxIterationen && attraktoren.Count > 0; iter++)
        {
            (int added, int killed) = Schritt(baum, attraktoren, o, rng, o.VermeideKnoten, avoidSq);
            if (added == 0 && killed == 0) break;
            if (baum.Knoten.Count > KnotenObergrenze) break;
        }

        return new SkelettErgebnis
        {
            Baum = baum,
            ZieleGesamt = gesamt,
            ZieleErreicht = gesamt - attraktoren.Count,
            Iterationen = iter,
        };
    }

    /// <summary>
    /// Zwei sich durchdringende, aber nie berührende Bäume (Spec 4.5).
    /// Beide wachsen verschränkt und weichen dem jeweils anderen mit
    /// Mindestabstand aus — genau wie die Browser-Vorschau.
    /// </summary>
    public DoppelErgebnis ErzeugeDoppel(
        Vector3 startA, IReadOnlyList<Target> zieleA,
        Vector3 startB, IReadOnlyList<Target> zieleB,
        EngineOptionen o, float minDist)
    {
        var attrA = SammleAttraktoren(zieleA);
        var attrB = SammleAttraktoren(zieleB);
        int gesA = attrA.Count, gesB = attrB.Count;

        var baumA = new Tree(new Node(0, startA));
        var baumB = new Tree(new Node(0, startB)) { BaumId = "zweiter" };
        var rng = new Random(o.Seed);
        float md2 = minDist * minDist;

        int iter = 0;
        for (; iter < o.MaxIterationen && (attrA.Count > 0 || attrB.Count > 0); iter++)
        {
            List<Vector3> snapA = baumA.Knoten.Select(n => n.Position).ToList();
            List<Vector3> snapB = baumB.Knoten.Select(n => n.Position).ToList();

            int aA = 0, kA = 0, aB = 0, kB = 0;
            if (attrA.Count > 0) (aA, kA) = Schritt(baumA, attrA, o, rng, snapB, md2);
            if (attrB.Count > 0) (aB, kB) = Schritt(baumB, attrB, o, rng, snapA, md2);

            if (aA + aB == 0 && kA + kB == 0) break;
            if (baumA.Knoten.Count + baumB.Knoten.Count > KnotenObergrenze) break;
        }

        return new DoppelErgebnis
        {
            BaumA = baumA, BaumB = baumB,
            ZieleGesamtA = gesA, ZieleErreichtA = gesA - attrA.Count,
            ZieleGesamtB = gesB, ZieleErreichtB = gesB - attrB.Count,
            Iterationen = iter,
        };
    }

    private static List<Vector3> SammleAttraktoren(IReadOnlyList<Target> ziele)
    {
        var liste = new List<Vector3>();
        foreach (Target t in ziele)
            liste.AddRange(t.Attraktorpunkte());
        return liste;
    }

    /// <summary>Ein Wachstumsschritt für einen Baum; gibt (neue Knoten, entfernte Ziele) zurück.</summary>
    private (int added, int killed) Schritt(
        Tree baum, List<Vector3> attraktoren, EngineOptionen o, Random rng,
        IReadOnlyList<Vector3>? avoid, float avoidSq)
    {
        float einflussSq = o.Einflussradius <= 0 ? float.MaxValue : o.Einflussradius * o.Einflussradius;
        float killSq = o.Killabstand * o.Killabstand;

        var richtung = new Dictionary<Node, Vector3>();
        foreach (Vector3 a in attraktoren)
        {
            Node? naechster = null;
            float bestSq = einflussSq;
            foreach (Node n in baum.Knoten)
            {
                float dSq = Vector3.DistanceSquared(n.Position, a);
                if (dSq < bestSq) { bestSq = dSq; naechster = n; }
            }
            if (naechster is null) continue;
            Vector3 dir = a - naechster.Position;
            if (dir.LengthSquared() > Epsilon) dir = Vector3.Normalize(dir);
            richtung[naechster] = richtung.TryGetValue(naechster, out Vector3 acc) ? acc + dir : dir;
        }

        var neue = new List<Node>();
        if (richtung.Count == 0)
        {
            Node? best = null;
            Vector3 bd = Vector3.Zero;
            float bestSq = float.MaxValue;
            foreach (Vector3 a in attraktoren)
                foreach (Node n in baum.Knoten)
                {
                    float dSq = Vector3.DistanceSquared(n.Position, a);
                    if (dSq < bestSq) { bestSq = dSq; best = n; bd = a - n.Position; }
                }
            if (best is null) return (0, 0);
            Vector3? np = Step(best.Position, bd, o, rng, avoid, avoidSq);
            if (np is Vector3 pp) neue.Add(baum.Verzweige(best, pp));
        }
        else
        {
            foreach (KeyValuePair<Node, Vector3> kv in richtung)
            {
                Vector3 dir = kv.Value;
                if (dir.LengthSquared() < Epsilon) continue;
                dir = Vector3.Normalize(dir);
                Vector3? np = Step(kv.Key.Position, dir, o, rng, avoid, avoidSq);
                if (np is Vector3 pp) neue.Add(baum.Verzweige(kv.Key, pp));
            }
        }

        int vorher = attraktoren.Count;
        attraktoren.RemoveAll(a =>
        {
            foreach (Node n in baum.Knoten)
                if (Vector3.DistanceSquared(n.Position, a) <= killSq)
                    return true;
            return false;
        });

        MarkiereBlaetter(baum);
        return (neue.Count, vorher - attraktoren.Count);
    }

    /// <summary>Jitter + Hindernis-Ausweichung + Doppelbaum-Abstand. null = Schritt blockiert.</summary>
    private Vector3? Step(Vector3 fromPos, Vector3 rawDir, EngineOptionen o, Random rng,
        IReadOnlyList<Vector3>? avoid, float avoidSq)
    {
        Vector3 d = rawDir.LengthSquared() > Epsilon ? Vector3.Normalize(rawDir) : rawDir;

        if (o.Jitter > 0f)
        {
            var j = new Vector3(
                (float)(rng.NextDouble() * 2 - 1),
                (float)(rng.NextDouble() * 2 - 1),
                (float)(rng.NextDouble() * 2 - 1)) * o.Jitter;
            Vector3 r = d + j;
            if (r.LengthSquared() > Epsilon) d = Vector3.Normalize(r);
        }

        foreach (Hindernis h in o.Hindernisse)
        {
            Vector3 probe = fromPos + d * o.Schrittweite;
            if (h.Innerhalb(probe, o.Clearance))
            {
                Vector3 inward = h.Zentrum - fromPos;
                if (inward.LengthSquared() > Epsilon) inward = Vector3.Normalize(inward);
                float comp = Vector3.Dot(d, inward);
                if (comp > 0f)
                {
                    Vector3 nd = d - inward * comp;
                    if (nd.LengthSquared() > Epsilon) d = Vector3.Normalize(nd);
                }
            }
        }

        Vector3 np = fromPos + d * o.Schrittweite;
        foreach (Hindernis h in o.Hindernisse)
            if (h.Innerhalb(np, o.Clearance)) return null;
        if (avoid is not null && avoidSq > 0f)
            foreach (Vector3 q in avoid)
                if (Vector3.DistanceSquared(np, q) < avoidSq) return null;
        return np;
    }

    private static void MarkiereBlaetter(Tree baum)
    {
        var hatKind = new HashSet<int>();
        foreach (Segment s in baum.Segmente)
            hatKind.Add(s.VonNode.Id);
        foreach (Node n in baum.Knoten)
            n.IstBlatt = !hatKind.Contains(n.Id);
    }
}
