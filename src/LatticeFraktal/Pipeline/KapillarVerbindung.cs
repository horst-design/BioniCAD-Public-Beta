using System.Numerics;
using LatticeFraktal.Model;

namespace LatticeFraktal.Pipeline;

/// <summary>
/// Verbindet die beiden Netze eines Doppelbaums durchgängig: JEDES Blattende
/// von A wird auf den nächsten Knoten von B gesetzt (und umgekehrt). Damit hat
/// jedes Ende eine Verbindung ins andere Netz — keine Sackgassen, das System
/// ist durchströmbar. reichweite begrenzt die Verbindungsdistanz; was darüber
/// liegt, bleibt eine Sackgasse (wird gezählt).
/// </summary>
public static class KapillarVerbindung
{
    public static (int Verbindungen, int Sackgassen) VerbindeAlleEnden(Tree a, Tree b, float reichweite)
    {
        float r2 = reichweite * reichweite;
        int verb = 0, dead = 0;

        var posB = new List<Vector3>();
        foreach (Node n in b.Knoten) posB.Add(n.Position);
        foreach (Node la in a.Knoten)
        {
            if (!la.IstBlatt) continue;
            int j = Naechster(posB, la.Position, r2);
            if (j >= 0) { la.Position = posB[j]; verb++; } else dead++;
        }

        var posA = new List<Vector3>();
        foreach (Node n in a.Knoten) posA.Add(n.Position);
        foreach (Node lb in b.Knoten)
        {
            if (!lb.IstBlatt) continue;
            int j = Naechster(posA, lb.Position, r2);
            if (j >= 0) { lb.Position = posA[j]; verb++; } else dead++;
        }

        return (verb, dead);
    }

    private static int Naechster(List<Vector3> punkte, Vector3 p, float r2)
    {
        int bi = -1;
        float bd = r2;
        for (int i = 0; i < punkte.Count; i++)
        {
            float d = Vector3.DistanceSquared(punkte[i], p);
            if (d < bd) { bd = d; bi = i; }
        }
        return bi;
    }
}
