using System.Numerics;
using LatticeFraktal.Model;

namespace LatticeFraktal.Pipeline;

/// <summary>Nachgeschalteter Schritt: weist dem Skelett finale Durchmesser zu (Spec 1.4).</summary>
public interface IDurchmesserZuweisung
{
    void Zuweisen(Tree baum, float startDurchmesser, IReadOnlyList<Target> ziele, float exponent, float minDurchmesser);
}

/// <summary>
/// Durchmesser-Zuweisung nach dem verallgemeinerten Hess-Murray-Gesetz
/// (Spec 4.1 + 4.2): D_parent³ = Σ D_daughter³.
///
/// Idee: Jedes Blatt führt einen Fluss Q (= Gewicht des nächstgelegenen Ziels).
/// Der Fluss eines inneren Knotens ist die Summe der Kinderflüsse. Da nach
/// Murray Q ∝ D³ gilt, folgt aus Q_parent = Σ Q_child automatisch
/// D_parent³ = Σ D_child³ — das Gesetz ist damit exakt erfüllt.
///
/// Skalierung: Der Durchmesser wird so normiert, dass die Wurzel exakt den
/// vorgegebenen Startdurchmesser erhält. Das setzt Horsts Vorgabe
/// "Start groß, Ziel klein" direkt um.
/// </summary>
public sealed class MurrayDurchmesser : IDurchmesserZuweisung
{
    public void Zuweisen(Tree baum, float startDurchmesser, IReadOnlyList<Target> ziele, float exponent, float minDurchmesser)
    {
        if (exponent < 1f) exponent = 3f;
        // Attraktorpunkte mit ihrem Gewicht (für die Blatt-Flusszuordnung)
        var attraktoren = new List<(Vector3 Pos, float Gewicht)>();
        foreach (Target t in ziele)
            foreach (Vector3 pt in t.Attraktorpunkte())
                attraktoren.Add((pt, t.Gewicht));

        // Kind-Beziehungen aus den (gerichteten) Segmenten aufbauen
        var kinder = new Dictionary<int, List<Node>>();
        foreach (Node n in baum.Knoten)
            kinder[n.Id] = new List<Node>();
        foreach (Segment s in baum.Segmente)
            kinder[s.VonNode.Id].Add(s.ZuNode);

        // Fluss pro Knoten per Post-Order bestimmen
        var fluss = new Dictionary<int, float>();
        float qWurzel = BerechneFluss(baum.Wurzel, kinder, attraktoren, fluss);
        if (qWurzel <= 0f)
            qWurzel = 1f;

        // Durchmesser = Start * (Q / Q_Wurzel)^(1/Exponent), nach unten begrenzt
        foreach (Node n in baum.Knoten)
            n.Durchmesser = MathF.Max(
                startDurchmesser * MathF.Pow(fluss[n.Id] / qWurzel, 1f / exponent),
                minDurchmesser);

        // Segmentdurchmesser = Durchmesser des kindseitigen Knotens
        foreach (Segment s in baum.Segmente)
            s.Durchmesser = s.ZuNode.Durchmesser;
    }

    private static float BerechneFluss(
        Node knoten,
        Dictionary<int, List<Node>> kinder,
        List<(Vector3 Pos, float Gewicht)> attraktoren,
        Dictionary<int, float> fluss)
    {
        List<Node> kids = kinder[knoten.Id];
        if (kids.Count == 0)
        {
            float q = NaechstesGewicht(knoten.Position, attraktoren);
            fluss[knoten.Id] = q;
            return q;
        }

        float summe = 0f;
        foreach (Node k in kids)
            summe += BerechneFluss(k, kinder, attraktoren, fluss);
        fluss[knoten.Id] = summe;
        return summe;
    }

    private static float NaechstesGewicht(Vector3 pos, List<(Vector3 Pos, float Gewicht)> attraktoren)
    {
        if (attraktoren.Count == 0)
            return 1f;

        float bestSq = float.MaxValue;
        float gewicht = 1f;
        foreach ((Vector3 p, float g) in attraktoren)
        {
            float dSq = Vector3.DistanceSquared(pos, p);
            if (dSq < bestSq)
            {
                bestSq = dSq;
                gewicht = g;
            }
        }
        return gewicht;
    }
}
