using System.Numerics;
using LatticeFraktal.Model;

namespace LatticeFraktal.Pipeline;

/// <summary>
/// Laplace-Glättung des Skeletts: Jeder innere Knoten wird Richtung Mittelwert
/// seiner Nachbarn (Eltern + Kinder) verschoben. Wurzel und Blätter bleiben fix
/// (damit Einlass und Zielpunkte erhalten bleiben). Entspricht 1:1 der Glättung
/// in der Browser-Vorschau.
/// </summary>
public static class SkelettGlaettung
{
    public static void Glaette(Tree baum, int paesse, float staerke)
    {
        if (paesse <= 0 || staerke <= 0f)
            return;

        var kinder = new Dictionary<int, List<Node>>();
        foreach (Node n in baum.Knoten)
            kinder[n.Id] = new List<Node>();
        var eltern = new Dictionary<int, Node>();
        foreach (Segment s in baum.Segmente)
        {
            kinder[s.VonNode.Id].Add(s.ZuNode);
            eltern[s.ZuNode.Id] = s.VonNode;
        }

        for (int p = 0; p < paesse; p++)
        {
            var neu = new Dictionary<int, Vector3>();
            foreach (Node n in baum.Knoten)
            {
                List<Node> kids = kinder[n.Id];
                if (n.IstWurzel || kids.Count == 0)
                {
                    neu[n.Id] = n.Position;
                    continue;
                }

                Vector3 summe = eltern[n.Id].Position;
                int anzahl = 1;
                foreach (Node k in kids)
                {
                    summe += k.Position;
                    anzahl++;
                }
                Vector3 mittel = summe / anzahl;
                neu[n.Id] = n.Position + (mittel - n.Position) * staerke;
            }

            foreach (Node n in baum.Knoten)
                n.Position = neu[n.Id];
        }
    }
}
