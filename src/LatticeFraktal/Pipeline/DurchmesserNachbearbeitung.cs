using LatticeFraktal.Model;

namespace LatticeFraktal.Pipeline;

/// <summary>
/// Nachbearbeitung der Durchmesser (gespiegelt aus der Browser-Vorschau):
/// Glättung entlang der Äste und Begrenzung des Volumenverhältnisses pro
/// Verzweigung. Beide weichen bewusst etwas vom exakten Murray-Gesetz ab und
/// sind über Parameter steuerbar (0 / 50 = praktisch aus).
/// </summary>
public static class DurchmesserNachbearbeitung
{
    /// <summary>Laplace-Glättung der Durchmesser (Mittel aus Eltern + Kindern). Wurzel fix.</summary>
    public static void Glaette(Tree baum, int paesse, float staerke)
    {
        if (paesse <= 0 || staerke <= 0f) return;

        var kinder = new Dictionary<int, List<Node>>();
        foreach (Node n in baum.Knoten) kinder[n.Id] = new List<Node>();
        var eltern = new Dictionary<int, Node>();
        foreach (Segment s in baum.Segmente)
        {
            kinder[s.VonNode.Id].Add(s.ZuNode);
            eltern[s.ZuNode.Id] = s.VonNode;
        }

        for (int p = 0; p < paesse; p++)
        {
            var alt = new Dictionary<int, float>();
            foreach (Node n in baum.Knoten) alt[n.Id] = n.Durchmesser;

            foreach (Node n in baum.Knoten)
            {
                if (n.IstWurzel) continue;
                float summe = alt[eltern[n.Id].Id];
                int anzahl = 1;
                foreach (Node k in kinder[n.Id]) { summe += alt[k.Id]; anzahl++; }
                float mittel = summe / anzahl;
                n.Durchmesser = alt[n.Id] + (mittel - alt[n.Id]) * staerke;
            }
        }

        foreach (Segment s in baum.Segmente) s.Durchmesser = s.ZuNode.Durchmesser;
    }

    /// <summary>
    /// Begrenzt pro Verzweigung das Volumenverhältnis: D_kind ≥ D_eltern / volRatio^(1/3).
    /// volRatio = 1 => alle Kinder so dick wie der Elternast.
    /// </summary>
    public static void BegrenzeVolumenverhaeltnis(Tree baum, float volRatio)
    {
        float f = 1f / MathF.Cbrt(MathF.Max(volRatio, 1f));

        var eltern = new Dictionary<int, Node>();
        foreach (Segment s in baum.Segmente) eltern[s.ZuNode.Id] = s.VonNode;

        // Knoten sind in Erzeugungsreihenfolge (Eltern.Id < Kind.Id) -> top-down korrekt.
        foreach (Node n in baum.Knoten)
        {
            if (!eltern.TryGetValue(n.Id, out Node? par)) continue;
            float pd = par.Durchmesser;
            n.Durchmesser = MathF.Max(MathF.Min(n.Durchmesser, pd), pd * f);
        }

        foreach (Segment s in baum.Segmente) s.Durchmesser = s.ZuNode.Durchmesser;
    }
}
